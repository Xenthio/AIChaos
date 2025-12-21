using Stripe;
using Stripe.Checkout;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for handling Stripe payments and checkout sessions.
/// This provides on-site payment without requiring users to manually enter usernames.
/// </summary>
public class StripeService
{
    private readonly SettingsService _settingsService;
    private readonly AccountService _accountService;
    private readonly ILogger<StripeService> _logger;
    private readonly HashSet<string> _processedPayments = new();
    private readonly object _lock = new();

    public StripeService(
        SettingsService settingsService,
        AccountService accountService,
        ILogger<StripeService> logger)
    {
        _settingsService = settingsService;
        _accountService = accountService;
        _logger = logger;
    }

    /// <summary>
    /// Initializes Stripe with the secret key from settings.
    /// </summary>
    private void InitializeStripe()
    {
        var settings = _settingsService.Settings.PaymentProviders.Stripe;
        if (!string.IsNullOrEmpty(settings.SecretKey))
        {
            StripeConfiguration.ApiKey = settings.SecretKey;
        }
    }

    /// <summary>
    /// Creates a Stripe Checkout session for a user to add credits.
    /// Returns the checkout session URL to redirect the user to.
    /// </summary>
    /// <param name="accountId">The user's account ID</param>
    /// <param name="amount">Amount in USD to charge</param>
    /// <param name="successUrl">URL to redirect after successful payment</param>
    /// <param name="cancelUrl">URL to redirect if payment is cancelled</param>
    public async Task<ServiceResult<string>> CreateCheckoutSessionAsync(
        string accountId, 
        decimal amount,
        string successUrl,
        string cancelUrl)
    {
        var settings = _settingsService.Settings.PaymentProviders.Stripe;

        if (!settings.Enabled)
        {
            return ServiceResult<string>.Fail("Stripe integration is not enabled");
        }

        if (amount < settings.MinPaymentAmount)
        {
            return ServiceResult<string>.Fail($"Payment amount must be at least ${settings.MinPaymentAmount}");
        }

        var account = _accountService.GetAccountById(accountId);
        if (account == null)
        {
            return ServiceResult<string>.Fail("Account not found");
        }

        try
        {
            InitializeStripe();

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "AIChaos Credits",
                                Description = $"${amount:F2} in credits for submitting Ideas"
                            },
                            UnitAmount = (long)(amount * 100), // Stripe uses cents
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                ClientReferenceId = accountId, // Store account ID to link payment back
                CustomerEmail = account.Username + "@aichaos.local", // Optional: helps prevent fraud
                Metadata = new Dictionary<string, string>
                {
                    { "account_id", accountId },
                    { "username", account.Username },
                    { "amount_usd", amount.ToString("F2") }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            _logger.LogInformation("[Stripe] Created checkout session {SessionId} for {Username} (${Amount})",
                session.Id, account.Username, amount);

            return ServiceResult<string>.Ok(session.Url, "Checkout session created successfully");
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "[Stripe] Failed to create checkout session for {AccountId}", accountId);
            return ServiceResult<string>.Fail($"Payment processor error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Stripe] Unexpected error creating checkout session");
            return ServiceResult<string>.Fail("Failed to create payment session");
        }
    }

    /// <summary>
    /// Processes a Stripe webhook event (payment confirmation, etc.).
    /// </summary>
    public ServiceResult<PaymentWebhookResponse> ProcessWebhook(string json, string signatureHeader)
    {
        var settings = _settingsService.Settings.PaymentProviders.Stripe;

        if (!settings.Enabled)
        {
            _logger.LogWarning("[Stripe] Received webhook but Stripe integration is disabled");
            return ServiceResult<PaymentWebhookResponse>.Fail("Stripe integration is not enabled");
        }

        if (string.IsNullOrEmpty(settings.WebhookSecret))
        {
            _logger.LogError("[Stripe] Webhook secret not configured");
            return ServiceResult<PaymentWebhookResponse>.Fail("Webhook secret not configured");
        }

        try
        {
            InitializeStripe();

            // Verify webhook signature
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                settings.WebhookSecret
            );

            _logger.LogInformation("[Stripe] Received webhook event: {EventType}", stripeEvent.Type);

            // Handle different event types
            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Session;
                if (session == null)
                {
                    return ServiceResult<PaymentWebhookResponse>.Fail("Invalid session data");
                }

                return ProcessCheckoutSessionCompleted(session);
            }
            else if (stripeEvent.Type == "checkout.session.expired")
            {
                _logger.LogInformation("[Stripe] Checkout session expired: {SessionId}", 
                    ((Session)stripeEvent.Data.Object).Id);
                return ServiceResult<PaymentWebhookResponse>.Ok(new PaymentWebhookResponse
                {
                    Status = "expired",
                    Message = "Checkout session expired"
                });
            }
            else
            {
                _logger.LogInformation("[Stripe] Unhandled event type: {EventType}", stripeEvent.Type);
                return ServiceResult<PaymentWebhookResponse>.Ok(new PaymentWebhookResponse
                {
                    Status = "ignored",
                    Message = $"Event type {stripeEvent.Type} not handled"
                });
            }
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "[Stripe] Webhook signature verification failed");
            return ServiceResult<PaymentWebhookResponse>.Fail("Invalid webhook signature");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Stripe] Failed to process webhook");
            return ServiceResult<PaymentWebhookResponse>.Fail("Failed to process webhook");
        }
    }

    /// <summary>
    /// Processes a completed checkout session by adding credits to the user's account.
    /// </summary>
    private ServiceResult<PaymentWebhookResponse> ProcessCheckoutSessionCompleted(Session session)
    {
        // Check for duplicate processing
        lock (_lock)
        {
            if (_processedPayments.Contains(session.Id))
            {
                _logger.LogInformation("[Stripe] Duplicate session {SessionId} ignored", session.Id);
                return ServiceResult<PaymentWebhookResponse>.Fail("Duplicate session");
            }
            _processedPayments.Add(session.Id);
        }

        // Get account ID from session metadata
        var accountId = session.ClientReferenceId ?? session.Metadata?.GetValueOrDefault("account_id");
        if (string.IsNullOrEmpty(accountId))
        {
            _logger.LogError("[Stripe] Session {SessionId} missing account ID", session.Id);
            return ServiceResult<PaymentWebhookResponse>.Fail("Missing account ID in session");
        }

        var account = _accountService.GetAccountById(accountId);
        if (account == null)
        {
            _logger.LogError("[Stripe] Account {AccountId} not found for session {SessionId}", 
                accountId, session.Id);
            return ServiceResult<PaymentWebhookResponse>.Fail("Account not found");
        }

        // Calculate credits from payment amount
        var amountPaidCents = session.AmountTotal ?? 0;
        var amountPaidUsd = amountPaidCents / 100m;

        if (amountPaidUsd <= 0)
        {
            _logger.LogWarning("[Stripe] Session {SessionId} has zero or negative amount", session.Id);
            return ServiceResult<PaymentWebhookResponse>.Fail("Invalid payment amount");
        }

        // Add credits to account
        _accountService.AddCredits(accountId, amountPaidUsd);

        _logger.LogInformation("[Stripe] Added ${Amount} credits to {Username} from session {SessionId}",
            amountPaidUsd, account.Username, session.Id);

        return ServiceResult<PaymentWebhookResponse>.Ok(new PaymentWebhookResponse
        {
            Status = "success",
            Message = $"Successfully added ${amountPaidUsd:F2} credits to {account.Username}",
            CreditsAdded = amountPaidUsd,
            Username = account.Username
        });
    }

    /// <summary>
    /// Gets statistics about Stripe integration.
    /// </summary>
    public StripeStatistics GetStatistics()
    {
        return new StripeStatistics
        {
            IsEnabled = _settingsService.Settings.PaymentProviders.Stripe.Enabled,
            ProcessedPaymentCount = _processedPayments.Count
        };
    }

    /// <summary>
    /// Retrieves details of a checkout session by ID.
    /// </summary>
    public async Task<Session?> GetSessionAsync(string sessionId)
    {
        try
        {
            InitializeStripe();
            var service = new SessionService();
            return await service.GetAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Stripe] Failed to retrieve session {SessionId}", sessionId);
            return null;
        }
    }
}

/// <summary>
/// Statistics about Stripe integration.
/// </summary>
public class StripeStatistics
{
    public bool IsEnabled { get; set; }
    public int ProcessedPaymentCount { get; set; }
}
