using System.Text.Json;
using System.Text.RegularExpressions;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for handling Ko-fi webhook donations and credit processing.
/// </summary>
public partial class KofiService
{
    private readonly SettingsService _settingsService;
    private readonly AccountService _accountService;
    private readonly ILogger<KofiService> _logger;
    private readonly HashSet<string> _processedTransactions = new();
    private readonly object _lock = new();

    public KofiService(
        SettingsService settingsService,
        AccountService accountService,
        ILogger<KofiService> logger)
    {
        _settingsService = settingsService;
        _accountService = accountService;
        _logger = logger;
    }

    /// <summary>
    /// Processes a Ko-fi webhook payload and adds credits to the user's account.
    /// </summary>
    /// <param name="payload">The Ko-fi webhook payload</param>
    /// <returns>Result with status message and credits added</returns>
    public ServiceResult<PaymentWebhookResponse> ProcessDonation(KofiWebhookPayload payload)
    {
        var settings = _settingsService.Settings.PaymentProviders.Kofi;

        // Validate Ko-fi is enabled
        if (!settings.Enabled)
        {
            _logger.LogWarning("[Ko-fi] Received webhook but Ko-fi integration is disabled");
            return ServiceResult<PaymentWebhookResponse>.Fail("Ko-fi integration is not enabled");
        }

        // Verify the verification token
        if (string.IsNullOrEmpty(settings.VerificationToken))
        {
            _logger.LogError("[Ko-fi] Verification token not configured");
            return ServiceResult<PaymentWebhookResponse>.Fail("Ko-fi verification token not configured");
        }

        if (payload.VerificationToken != settings.VerificationToken)
        {
            _logger.LogWarning("[Ko-fi] Invalid verification token received");
            return ServiceResult<PaymentWebhookResponse>.Fail("Invalid verification token");
        }

        // Check for duplicate transaction
        var transactionId = payload.KofiTransactionId ?? payload.MessageId;
        if (string.IsNullOrEmpty(transactionId))
        {
            _logger.LogWarning("[Ko-fi] Webhook missing transaction ID");
            return ServiceResult<PaymentWebhookResponse>.Fail("Missing transaction ID");
        }

        lock (_lock)
        {
            if (_processedTransactions.Contains(transactionId))
            {
                _logger.LogInformation("[Ko-fi] Duplicate transaction {TransactionId} ignored", transactionId);
                return ServiceResult<PaymentWebhookResponse>.Fail("Duplicate transaction");
            }
            _processedTransactions.Add(transactionId);
        }

        // Parse donation amount
        if (!decimal.TryParse(payload.Amount, out var amount) || amount <= 0)
        {
            _logger.LogWarning("[Ko-fi] Invalid donation amount: {Amount}", payload.Amount);
            return ServiceResult<PaymentWebhookResponse>.Fail("Invalid donation amount");
        }

        // Check minimum donation
        if (amount < settings.MinDonationAmount)
        {
            _logger.LogInformation("[Ko-fi] Donation {Amount} below minimum {Min}",
                amount, settings.MinDonationAmount);
            return ServiceResult<PaymentWebhookResponse>.Fail(
                $"Donation amount must be at least ${settings.MinDonationAmount}");
        }

        // Extract username from message
        var username = ExtractUsernameFromMessage(payload.Message ?? "");
        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("[Ko-fi] No username found in donation message. From: {FromName}, Message: {Message}",
                payload.FromName, payload.Message);
            
            // Store as pending credits with Ko-fi transaction ID
            _accountService.AddPendingCreditsForUnknownUser(
                transactionId,
                amount,
                payload.FromName ?? "Unknown",
                payload.Message ?? "",
                "ko-fi");
            
            return ServiceResult<PaymentWebhookResponse>.Ok(new PaymentWebhookResponse
            {
                Status = "pending",
                Message = "Donation received but username not found in message. Credits will be added when user links account.",
                CreditsAdded = amount
            });
        }

        // Find account by username
        var account = _accountService.GetAccountByUsername(username);
        if (account == null)
        {
            _logger.LogWarning("[Ko-fi] Username '{Username}' not found", username);
            
            // Store as pending credits
            _accountService.AddPendingCreditsForUnknownUser(
                transactionId,
                amount,
                payload.FromName ?? username,
                payload.Message ?? "",
                "ko-fi");
            
            return ServiceResult<PaymentWebhookResponse>.Ok(new PaymentWebhookResponse
            {
                Status = "pending",
                Message = $"Username '{username}' not found. Credits will be added when account is created.",
                CreditsAdded = amount,
                Username = username
            });
        }

        // Add credits to account
        _accountService.AddCredits(account.Id, amount);

        _logger.LogInformation("[Ko-fi] Added {Amount} credits to {Username} from donation by {FromName}",
            amount, username, payload.FromName);

        return ServiceResult<PaymentWebhookResponse>.Ok(new PaymentWebhookResponse
        {
            Status = "success",
            Message = $"Successfully added ${amount} credits to {username}",
            CreditsAdded = amount,
            Username = username
        });
    }

    /// <summary>
    /// Extracts username from Ko-fi donation message using various patterns.
    /// Supports formats like:
    /// - "username: JohnDoe"
    /// - "user: JohnDoe"
    /// - "for: JohnDoe"
    /// - "account: JohnDoe"
    /// - "@JohnDoe"
    /// - Just "JohnDoe" if it looks like a username
    /// </summary>
    private string? ExtractUsernameFromMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        // Pattern 1: "username: JohnDoe" or "user: JohnDoe" (case-insensitive)
        var match = UsernameColonPattern().Match(message);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Pattern 2: "for: JohnDoe" or "account: JohnDoe"
        match = ForAccountPattern().Match(message);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Pattern 3: "@JohnDoe"
        match = AtUsernamePattern().Match(message);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Pattern 4: Try to extract first word if it looks like a username (alphanumeric, 3-20 chars)
        var words = message.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (UsernameFormatPattern().IsMatch(word))
            {
                return word.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Gets statistics about Ko-fi donations.
    /// </summary>
    public KofiStatistics GetStatistics()
    {
        // This could be extended to track more detailed stats
        return new KofiStatistics
        {
            IsEnabled = _settingsService.Settings.PaymentProviders.Kofi.Enabled,
            ProcessedTransactionCount = _processedTransactions.Count
        };
    }

    [GeneratedRegex(@"(?:username|user)\s*:\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex UsernameColonPattern();
    
    [GeneratedRegex(@"(?:for|account)\s*:\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex ForAccountPattern();
    
    [GeneratedRegex(@"@(\w+)")]
    private static partial Regex AtUsernamePattern();
    
    [GeneratedRegex(@"^[a-zA-Z0-9_]{3,20}$")]
    private static partial Regex UsernameFormatPattern();
}

/// <summary>
/// Statistics about Ko-fi integration.
/// </summary>
public class KofiStatistics
{
    public bool IsEnabled { get; set; }
    public int ProcessedTransactionCount { get; set; }
}
