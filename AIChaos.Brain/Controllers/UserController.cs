using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChaos.Brain.Controllers;

/// <summary>
/// Controller for user-related API endpoints (credits, submissions).
/// </summary>
[ApiController]
[Route("api/user")]
public class UserController : ControllerBase
{
    private readonly AccountService _accountService;
    private readonly CommandQueueService _commandQueue;
    private readonly AiCodeGeneratorService _codeGenerator;
    private readonly TestClientService _testClientService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        AccountService accountService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        TestClientService testClientService,
        ILogger<UserController> logger)
    {
        _accountService = accountService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _testClientService = testClientService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the credit balance for a user.
    /// </summary>
    [HttpGet("balance")]
    public ActionResult GetBalance([FromHeader(Name = "X-User-Id")] string accountId)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            return BadRequest(new { status = "error", message = "Account ID required" });
        }

        var account = _accountService.GetAccountBySession(accountId);
        var balance = account?.CreditBalance ?? 0m;
        return Ok(new { balance });
    }

    /// <summary>
    /// Submits a command using user credits.
    /// </summary>
    [HttpPost("submit")]
    public async Task<ActionResult> SubmitCommand(
        [FromHeader(Name = "X-User-Id")] string accountId,
        [FromHeader(Name = "X-User-Name")] string? userName,
        [FromBody] UserSubmitRequest request)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            return BadRequest(new { status = "error", message = "Account ID required" });
        }

        if (string.IsNullOrEmpty(request.Prompt))
        {
            return BadRequest(new { status = "error", message = "Prompt required" });
        }

        // Check rate limit
        var (allowed, waitSeconds) = _accountService.CheckRateLimit(accountId);
        if (!allowed)
        {
            return Ok(new
            {
                status = "error",
                message = $"Please wait {waitSeconds:F0} seconds before submitting another command.",
                rateLimited = true,
                waitSeconds
            });
        }

        // Check if account has enough credits
        var account = _accountService.GetAccountBySession(accountId);
        var balance = account?.CreditBalance ?? 0m;

        if (balance < Constants.CommandCost)
        {
            return Ok(new
            {
                status = "error",
                message = $"Insufficient credits. You have ${balance:F2}, but need ${Constants.CommandCost:F2}",
                balance
            });
        }

        try
        {
            // Generate code
            var (executionCode, undoCode) = await _codeGenerator.GenerateCodeAsync(request.Prompt);

            // Deduct credits
            if (account == null || !_accountService.DeductCredits(account.Id, Constants.CommandCost))
            {
                return Ok(new { status = "error", message = "Failed to deduct credits" });
            }

            // Add to command queue
            var isTestClientModeEnabled = _testClientService.IsEnabled;
            var entry = _commandQueue.AddCommand(
                request.Prompt,
                executionCode,
                undoCode,
                "web",
                userName ?? account.DisplayName ?? "User",
                null,
                account.Id,
                null,
                queueForExecution: !isTestClientModeEnabled);

            // If test client mode is enabled, queue for testing
            if (isTestClientModeEnabled)
            {
                _testClientService.QueueForTesting(entry.Id, executionCode, request.Prompt);
                _logger.LogInformation("[USER] Command #{CommandId} queued for testing by {AccountId}", entry.Id, account.Id);
            }
            else
            {
                _logger.LogInformation("[USER] Command #{CommandId} queued by {AccountId}", entry.Id, account.Id);
            }

            return Ok(new
            {
                status = "success",
                message = "Command submitted successfully",
                commandId = entry.Id,
                balance = account.CreditBalance,
                cost = Constants.CommandCost
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[USER] Failed to submit command for {AccountId}", accountId);
            return StatusCode(500, new { status = "error", message = "Failed to process command" });
        }
    }
    
    /// <summary>
    /// Simulates a Super Chat to add credits to an account's balance (admin only).
    /// </summary>
    [HttpPost("simulate-superchat")]
    public ActionResult SimulateSuperChat([FromBody] SimulateSuperChatRequest request)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return BadRequest(new { status = "error", message = "Username required" });
        }
        
        if (request.Amount <= 0)
        {
            return BadRequest(new { status = "error", message = "Amount must be positive" });
        }
        
        // Look up the account by username
        var account = _accountService.GetAccountByUsername(request.UserId);
        if (account == null)
        {
            return BadRequest(new { status = "error", message = $"Account '{request.UserId}' not found" });
        }
        
        var displayName = request.DisplayName ?? account.DisplayName ?? account.Username;
        
        // If the account has a linked YouTube channel, use that. Otherwise, use the username as channel ID.
        var channelId = account.LinkedYouTubeChannelId ?? account.Username;
        
        // Add credits to the channel (will go to account if linked, or store as pending)
        _accountService.AddCreditsToChannel(channelId, request.Amount, displayName, "Simulated Super Chat");
        
        // Get the updated account to retrieve new balance
        var updatedAccount = _accountService.GetAccountByUsername(request.UserId);
        var newBalance = updatedAccount?.CreditBalance ?? 0m;
        
        _logger.LogInformation("[ADMIN] Simulated Super Chat: ${Amount} to {User} ({Id})", 
            request.Amount, displayName, request.UserId);
        
        return Ok(new 
        { 
            status = "success", 
            message = $"Added ${request.Amount:F2} credits to {displayName}",
            newBalance
        });
    }
    
    /// <summary>
    /// Simulates a YouTube API Super Chat callback (admin only, for testing).
    /// This mimics what YouTubeService receives from YouTube's API.
    /// </summary>
    [HttpPost("simulate-youtube-api")]
    public ActionResult SimulateYouTubeApi([FromBody] SimulateYouTubeApiRequest request,
        [FromServices] YouTubeService youtubeService)
    {
        if (string.IsNullOrEmpty(request.ChannelId))
        {
            return BadRequest(new { status = "error", message = "YouTube Channel ID required" });
        }
        
        if (request.Amount <= 0)
        {
            return BadRequest(new { status = "error", message = "Amount must be positive" });
        }
        
        try
        {
            // Directly call AccountService like YouTubeService does
            _accountService.AddCreditsToChannel(
                request.ChannelId, 
                request.Amount, 
                request.DisplayName ?? "Test User",
                request.Message ?? "Test Super Chat");
            
            _logger.LogInformation("[ADMIN] Simulated YouTube API Super Chat: ${Amount} from channel {ChannelId}", 
                request.Amount, request.ChannelId);
            
            return Ok(new 
            { 
                status = "success", 
                message = $"Simulated ${request.Amount:F2} Super Chat from channel {request.ChannelId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ADMIN] Failed to simulate YouTube API call");
            return StatusCode(500, new { status = "error", message = "Simulation failed" });
        }
    }
}

/// <summary>
/// Request model for user command submission.
/// </summary>
public class UserSubmitRequest
{
    public string Prompt { get; set; } = "";
}

/// <summary>
/// Request model for simulating a Super Chat.
/// </summary>
public class SimulateSuperChatRequest
{
    public string UserId { get; set; } = "";
    public string? DisplayName { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Request model for simulating a YouTube API Super Chat callback.
/// </summary>
public class SimulateYouTubeApiRequest
{
    public string ChannelId { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Message { get; set; }
    public decimal Amount { get; set; }
}
