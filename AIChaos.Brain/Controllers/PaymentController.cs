using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using AIChaos.Brain.Models;
using AIChaos.Brain.Services;

namespace AIChaos.Brain.Controllers;

/// <summary>
/// Controller for handling payment provider webhooks (Ko-fi, Stripe, PayPal).
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentController : ControllerBase
{
    private readonly KofiService _kofiService;
    private readonly AccountService _accountService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        KofiService kofiService,
        AccountService accountService,
        ILogger<PaymentController> logger)
    {
        _kofiService = kofiService;
        _accountService = accountService;
        _logger = logger;
    }

    /// <summary>
    /// Ko-fi webhook endpoint.
    /// Receives donation notifications from Ko-fi.
    /// </summary>
    /// <remarks>
    /// Ko-fi sends webhook data as form-urlencoded with a "data" field containing JSON.
    /// Example: data={"verification_token":"...","message_id":"...","type":"Donation",...}
    /// </remarks>
    [HttpPost("kofi")]
    public async Task<IActionResult> KofiWebhook([FromForm] string data)
    {
        try
        {
            _logger.LogInformation("[Ko-fi Webhook] Received webhook");

            if (string.IsNullOrEmpty(data))
            {
                _logger.LogWarning("[Ko-fi Webhook] Empty data received");
                return BadRequest("Empty data");
            }

            // Parse the JSON payload
            var payload = JsonSerializer.Deserialize<KofiWebhookPayload>(data, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload == null)
            {
                _logger.LogWarning("[Ko-fi Webhook] Failed to parse payload");
                return BadRequest("Invalid payload");
            }

            _logger.LogInformation("[Ko-fi Webhook] Type: {Type}, From: {FromName}, Amount: {Amount} {Currency}",
                payload.Type, payload.FromName, payload.Amount, payload.Currency);

            // Process the donation
            var result = _kofiService.ProcessDonation(payload);

            if (result.Success)
            {
                return Ok(result.Data);
            }

            // Log error but return 200 OK to prevent Ko-fi from retrying
            // (We don't want duplicate processing attempts for invalid data)
            _logger.LogWarning("[Ko-fi Webhook] Processing failed: {Error}", result.Message);
            return Ok(new PaymentWebhookResponse
            {
                Status = "error",
                Message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Ko-fi Webhook] Exception processing webhook");
            
            // Return 200 OK to prevent retries
            return Ok(new PaymentWebhookResponse
            {
                Status = "error",
                Message = "Internal error processing webhook"
            });
        }
    }

    /// <summary>
    /// Gets Ko-fi integration status and statistics.
    /// </summary>
    [HttpGet("kofi/status")]
    public IActionResult GetKofiStatus()
    {
        var stats = _kofiService.GetStatistics();
        return Ok(new
        {
            enabled = stats.IsEnabled,
            processed_transactions = stats.ProcessedTransactionCount,
            webhook_url = $"{Request.Scheme}://{Request.Host}/api/payments/kofi"
        });
    }

    /// <summary>
    /// Gets all pending credits (admin only).
    /// </summary>
    [HttpGet("pending")]
    public IActionResult GetPendingCredits()
    {
        // TODO: Add admin authentication
        var pendingCredits = _accountService.GetAllPendingCredits();
        return Ok(pendingCredits);
    }

    /// <summary>
    /// Test endpoint to simulate a Ko-fi webhook (development only).
    /// </summary>
    [HttpPost("kofi/test")]
    public async Task<IActionResult> TestKofiWebhook([FromBody] KofiWebhookPayload payload)
    {
        _logger.LogInformation("[Ko-fi Test] Processing test webhook");
        
        var result = _kofiService.ProcessDonation(payload);
        
        if (result.Success)
        {
            return Ok(result.Data);
        }

        return BadRequest(new { error = result.Message });
    }
}
