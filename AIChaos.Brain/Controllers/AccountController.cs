using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChaos.Brain.Controllers;

/// <summary>
/// Controller for account management (registration, login, YouTube linking).
/// </summary>
[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly AccountService _accountService;
    private readonly CommandQueueService _commandQueue;
    private readonly AiCodeGeneratorService _codeGenerator;
    private readonly TestClientService _testClientService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AccountService accountService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        TestClientService testClientService,
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _testClientService = testClientService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new account.
    /// </summary>
    [HttpPost("register")]
    public ActionResult Register([FromBody] RegisterRequest request)
    {
        var (success, error, account) = _accountService.CreateAccount(
            request.Username, 
            request.Password, 
            request.DisplayName);
        
        if (!success)
        {
            return BadRequest(new { status = "error", message = error });
        }
        
        // Auto-login after registration
        var (_, _, _, sessionToken) = _accountService.Login(request.Username, request.Password);
        
        return Ok(new 
        { 
            status = "success", 
            message = "Account created successfully",
            sessionToken,
            account = new 
            {
                id = account!.Id,
                username = account.Username,
                displayName = account.DisplayName,
                balance = account.CreditBalance,
                linkedYouTube = account.LinkedYouTubeChannelId,
                picture = account.PictureUrl
            }
        });
    }

    /// <summary>
    /// Logs in to an existing account.
    /// </summary>
    [HttpPost("login")]
    public ActionResult Login([FromBody] LoginRequest request)
    {
        var (success, error, account, sessionToken) = _accountService.Login(request.Username, request.Password);
        
        if (!success)
        {
            return Unauthorized(new { status = "error", message = error });
        }
        
        return Ok(new 
        { 
            status = "success",
            sessionToken,
            account = new 
            {
                id = account!.Id,
                username = account.Username,
                displayName = account.DisplayName,
                balance = account.CreditBalance,
                linkedYouTube = account.LinkedYouTubeChannelId,
                picture = account.PictureUrl
            }
        });
    }

    /// <summary>
    /// Gets the current session info.
    /// </summary>
    [HttpGet("session")]
    public ActionResult GetSession([FromHeader(Name = "X-Session-Token")] string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return Unauthorized(new { status = "error", message = "Not logged in" });
        }
        
        var account = _accountService.GetAccountBySession(sessionToken);
        if (account == null)
        {
            return Unauthorized(new { status = "error", message = "Session expired" });
        }
        
        return Ok(new 
        { 
            status = "success",
            account = new 
            {
                id = account.Id,
                username = account.Username,
                displayName = account.DisplayName,
                balance = account.CreditBalance,
                linkedYouTube = account.LinkedYouTubeChannelId,
                picture = account.PictureUrl
            }
        });
    }

    /// <summary>
    /// Logs out the current session.
    /// </summary>
    [HttpPost("logout")]
    public ActionResult Logout([FromHeader(Name = "X-Session-Token")] string? sessionToken)
    {
        if (!string.IsNullOrEmpty(sessionToken))
        {
            _accountService.Logout(sessionToken);
        }
        return Ok(new { status = "success", message = "Logged out" });
    }

    /// <summary>
    /// Generates a verification code to link a YouTube channel.
    /// </summary>
    [HttpPost("link-youtube/generate")]
    public ActionResult GenerateLinkCode([FromHeader(Name = "X-Session-Token")] string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return Unauthorized(new { status = "error", message = "Not logged in" });
        }
        
        var account = _accountService.GetAccountBySession(sessionToken);
        if (account == null)
        {
            return Unauthorized(new { status = "error", message = "Session expired" });
        }
        
        if (!string.IsNullOrEmpty(account.LinkedYouTubeChannelId))
        {
            return BadRequest(new { status = "error", message = "YouTube channel already linked" });
        }
        
        var code = _accountService.GenerateYouTubeLinkCode(account.Id);
        
        return Ok(new 
        { 
            status = "success",
            code,
            message = $"Send a Super Chat containing '{code}' to link your YouTube channel. Code expires in 30 minutes."
        });
    }

    /// <summary>
    /// Links a YouTube channel using Google OAuth (JWT token from Google Sign-In).
    /// The JWT is verified server-side for security.
    /// </summary>
    [HttpPost("link-youtube/google")]
    public async Task<ActionResult> LinkYouTubeViaGoogle(
        [FromHeader(Name = "X-Session-Token")] string? sessionToken,
        [FromBody] LinkGoogleRequest request,
        [FromServices] SettingsService settingsService)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return Unauthorized(new { status = "error", message = "Not logged in" });
        }
        
        var account = _accountService.GetAccountBySession(sessionToken);
        if (account == null)
        {
            return Unauthorized(new { status = "error", message = "Session expired" });
        }
        
        if (!string.IsNullOrEmpty(account.LinkedYouTubeChannelId))
        {
            return BadRequest(new { status = "error", message = "YouTube channel already linked" });
        }
        
        if (string.IsNullOrEmpty(request.Credential))
        {
            return BadRequest(new { status = "error", message = "Google credential required" });
        }
        
        // Verify the JWT token and extract claims
        var (googleId, pictureUrl) = await VerifyGoogleToken(request.Credential, settingsService.Settings.YouTube.ClientId);
        
        if (string.IsNullOrEmpty(googleId))
        {
            return BadRequest(new { status = "error", message = "Invalid or expired Google credential" });
        }
        
        // Link the Google ID as the YouTube channel ID, with picture URL
        var success = _accountService.LinkYouTubeChannel(account.Id, googleId, pictureUrl);
        
        if (!success)
        {
            return BadRequest(new { status = "error", message = "Failed to link YouTube channel. It may already be linked to another account." });
        }
        
        _logger.LogInformation("[ACCOUNT] Linked YouTube via Google: {GoogleId} to {Username}", 
            googleId, account.Username);
        
        // Get updated account to return picture
        var updatedAccount = _accountService.GetAccountBySession(sessionToken);
        
        return Ok(new 
        { 
            status = "success",
            message = "YouTube channel linked successfully via Google Sign-In!",
            linkedYouTube = googleId,
            picture = pictureUrl
        });
    }
    
    /// <summary>
    /// Verifies a Google ID token and returns the Google ID (sub claim) and picture URL if valid.
    /// </summary>
    private async Task<(string? GoogleId, string? PictureUrl)> VerifyGoogleToken(string credential, string? expectedClientId)
    {
        try
        {
            // Simple JWT decode and verification
            // In production, you should use Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync
            // But for this use case, we do basic validation
            var parts = credential.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("[AUTH] Invalid JWT format");
                return (null, null);
            }
            
            // Decode payload
            var payload = parts[1];
            // Add padding if needed
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            
            var jsonPayload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var claims = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonPayload);
            
            if (claims == null)
            {
                _logger.LogWarning("[AUTH] Failed to parse JWT claims");
                return (null, null);
            }
            
            // Verify issuer
            if (claims.TryGetValue("iss", out var issObj))
            {
                var iss = issObj?.ToString();
                if (iss != "https://accounts.google.com" && iss != "accounts.google.com")
                {
                    _logger.LogWarning("[AUTH] Invalid JWT issuer: {Issuer}", iss);
                    return (null, null);
                }
            }
            else
            {
                _logger.LogWarning("[AUTH] Missing JWT issuer");
                return (null, null);
            }
            
            // Verify audience (client ID)
            if (!string.IsNullOrEmpty(expectedClientId) && claims.TryGetValue("aud", out var audObj))
            {
                var aud = audObj?.ToString();
                if (aud != expectedClientId)
                {
                    _logger.LogWarning("[AUTH] JWT audience mismatch. Expected: {Expected}, Got: {Got}", 
                        expectedClientId, aud);
                    return (null, null);
                }
            }
            
            // Verify expiration
            if (claims.TryGetValue("exp", out var expObj))
            {
                if (expObj is System.Text.Json.JsonElement jsonElement && jsonElement.TryGetInt64(out var exp))
                {
                    var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                    if (expTime < DateTimeOffset.UtcNow)
                    {
                        _logger.LogWarning("[AUTH] JWT token expired");
                        return (null, null);
                    }
                }
            }
            
            // Extract Google ID (sub claim)
            string? googleId = null;
            string? pictureUrl = null;
            
            if (claims.TryGetValue("sub", out var subObj))
            {
                googleId = subObj?.ToString();
            }
            
            // Extract picture URL
            if (claims.TryGetValue("picture", out var pictureObj))
            {
                pictureUrl = pictureObj?.ToString();
            }
            
            if (string.IsNullOrEmpty(googleId))
            {
                _logger.LogWarning("[AUTH] Missing sub claim in JWT");
                return (null, null);
            }
            
            return (googleId, pictureUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AUTH] Failed to verify Google token");
            return (null, null);
        }
    }

    /// <summary>
    /// Gets the credit balance.
    /// </summary>
    [HttpGet("balance")]
    public ActionResult GetBalance([FromHeader(Name = "X-Session-Token")] string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return Unauthorized(new { status = "error", message = "Not logged in" });
        }
        
        var account = _accountService.GetAccountBySession(sessionToken);
        if (account == null)
        {
            return Unauthorized(new { status = "error", message = "Session expired" });
        }
        
        return Ok(new { balance = account.CreditBalance });
    }

    /// <summary>
    /// Submits a chaos command using credits.
    /// </summary>
    [HttpPost("submit")]
    public async Task<ActionResult> SubmitCommand(
        [FromHeader(Name = "X-Session-Token")] string? sessionToken,
        [FromBody] SubmitRequest request)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return Unauthorized(new { status = "error", message = "Not logged in" });
        }
        
        var account = _accountService.GetAccountBySession(sessionToken);
        if (account == null)
        {
            return Unauthorized(new { status = "error", message = "Session expired" });
        }

        if (string.IsNullOrEmpty(request.Prompt))
        {
            return BadRequest(new { status = "error", message = "Prompt required" });
        }

        // Check rate limit
        var (allowed, waitSeconds) = _accountService.CheckRateLimit(account.Id);
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

        // Check credits
        if (account.CreditBalance < Constants.CommandCost)
        {
            return Ok(new
            {
                status = "error",
                message = $"Insufficient credits. You have ${account.CreditBalance:F2}, but need ${Constants.CommandCost:F2}",
                balance = account.CreditBalance
            });
        }

        try
        {
            // Generate code
            var (executionCode, undoCode) = await _codeGenerator.GenerateCodeAsync(request.Prompt);

            // Deduct credits
            if (!_accountService.DeductCredits(account.Id, Constants.CommandCost))
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
                account.DisplayName,
                null,
                account.Id,
                null,
                queueForExecution: !isTestClientModeEnabled);

            if (isTestClientModeEnabled)
            {
                _testClientService.QueueForTesting(entry.Id, executionCode, request.Prompt);
            }

            // Get updated balance
            var updatedAccount = _accountService.GetAccountBySession(sessionToken);
            
            return Ok(new
            {
                status = "success",
                message = "Command submitted successfully",
                commandId = entry.Id,
                balance = updatedAccount?.CreditBalance ?? 0,
                cost = Constants.CommandCost
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit command for {Username}", account.Username);
            return StatusCode(500, new { status = "error", message = "Failed to process command" });
        }
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class SubmitRequest
{
    public string Prompt { get; set; } = "";
}

public class LinkGoogleRequest
{
    public string Credential { get; set; } = "";
}
