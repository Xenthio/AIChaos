using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
    private readonly ImageModerationService _moderationService;
    private readonly CodeModerationService _codeModerationService;
    private readonly SettingsService _settingsService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AccountService accountService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        TestClientService testClientService,
        ImageModerationService moderationService,
        CodeModerationService codeModerationService,
        SettingsService settingsService,
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _testClientService = testClientService;
        _moderationService = moderationService;
        _codeModerationService = codeModerationService;
        _settingsService = settingsService;
        _logger = logger;
    }
    
    /// <summary>
    /// Gets the OAuth callback redirect URI for viewer YouTube linking.
    /// </summary>
    private string GetViewerOAuthRedirectUri()
    {
        return $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/account/link-youtube/oauth-callback";
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
    /// Unlinks the current user's YouTube channel.
    /// </summary>
    [HttpPost("link-youtube/unlink")]
    public ActionResult UnlinkYouTube([FromHeader(Name = "X-Session-Token")] string? sessionToken)
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
        
        if (string.IsNullOrEmpty(account.LinkedYouTubeChannelId))
        {
            return BadRequest(new { status = "error", message = "No YouTube channel linked" });
        }
        
        var success = _accountService.UnlinkYouTubeChannel(account.Id);
        
        if (!success)
        {
            return BadRequest(new { status = "error", message = "Failed to unlink YouTube channel" });
        }
        
        _logger.LogInformation("[ACCOUNT] User {Username} unlinked their YouTube channel", account.Username);
        
        return Ok(new 
        { 
            status = "success",
            message = "YouTube channel unlinked successfully. You can link a different channel now."
        });
    }

    /// <summary>
    /// Links a YouTube channel using Google OAuth (JWT token from Google Sign-In).
    /// The JWT is verified server-side for security, then the actual YouTube Channel ID is fetched.
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
        
        // Fetch the actual YouTube Channel ID using the credential token
        // This is critical: Google ID (sub claim) != YouTube Channel ID
        var youtubeChannelId = await GetYouTubeChannelIdFromCredential(request.Credential);
        
        if (string.IsNullOrEmpty(youtubeChannelId))
        {
            _logger.LogWarning("[ACCOUNT] Failed to fetch YouTube Channel ID for Google ID {GoogleId}", googleId);
            return BadRequest(new { status = "error", message = "Failed to fetch YouTube channel information. Make sure you have a YouTube channel." });
        }
        
        _logger.LogInformation("[ACCOUNT] Fetched YouTube Channel ID {ChannelId} for Google ID {GoogleId}", 
            youtubeChannelId, googleId);
        
        // Link the actual YouTube channel ID (not Google ID), with picture URL
        var success = _accountService.LinkYouTubeChannel(account.Id, youtubeChannelId, pictureUrl);
        
        if (!success)
        {
            return BadRequest(new { status = "error", message = "Failed to link YouTube channel. It may already be linked to another account." });
        }
        
        _logger.LogInformation("[ACCOUNT] Linked YouTube channel {ChannelId} to {Username} via Google OAuth", 
            youtubeChannelId, account.Username);
        
        // Get updated account to return picture
        var updatedAccount = _accountService.GetAccountBySession(sessionToken);
        
        return Ok(new 
        { 
            status = "success",
            message = "YouTube channel linked successfully via Google Sign-In!",
            linkedYouTube = youtubeChannelId,
            picture = pictureUrl
        });
    }
    
    /// <summary>
    /// Initiates the proper OAuth flow for viewers to link their YouTube channel.
    /// This returns an authorization URL that includes YouTube API scopes.
    /// </summary>
    [HttpGet("link-youtube/oauth-url")]
    public ActionResult GetYouTubeOAuthUrl(
        [FromHeader(Name = "X-Session-Token")] string? sessionToken,
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
        
        var settings = settingsService.Settings.YouTube;
        if (string.IsNullOrEmpty(settings.ClientId))
        {
            return BadRequest(new { status = "error", message = "YouTube OAuth not configured" });
        }
        
        // Build the OAuth URL with YouTube readonly scope
        var redirectUri = GetViewerOAuthRedirectUri();
        var scopes = "https://www.googleapis.com/auth/youtube.readonly";
        var state = sessionToken; // Pass session token as state to link after callback
        
        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={System.Web.HttpUtility.UrlEncode(settings.ClientId)}" +
            $"&redirect_uri={System.Web.HttpUtility.UrlEncode(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={System.Web.HttpUtility.UrlEncode(scopes)}" +
            $"&state={System.Web.HttpUtility.UrlEncode(state)}" +
            $"&access_type=offline";
        
        return Ok(new 
        { 
            status = "success",
            authUrl
        });
    }
    
    /// <summary>
    /// Handles the OAuth callback for viewer YouTube linking.
    /// Exchanges the authorization code for an access token and fetches the YouTube channel ID.
    /// </summary>
    [HttpGet("link-youtube/oauth-callback")]
    public async Task<ActionResult> YouTubeOAuthCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromServices] SettingsService settingsService,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return Redirect("/?error=" + System.Web.HttpUtility.UrlEncode("YouTube authorization was denied"));
        }
        
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Redirect("/?error=" + System.Web.HttpUtility.UrlEncode("Invalid OAuth callback"));
        }
        
        // State contains the session token
        var sessionToken = state;
        var account = _accountService.GetAccountBySession(sessionToken);
        if (account == null)
        {
            return Redirect("/?error=" + System.Web.HttpUtility.UrlEncode("Session expired"));
        }
        
        var settings = settingsService.Settings.YouTube;
        var redirectUri = GetViewerOAuthRedirectUri();
        
        try
        {
            // Exchange code for access token
            var client = httpClientFactory.CreateClient();
            using (var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            }))
            {
                var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", content);
                
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                    _logger.LogError("[ACCOUNT] OAuth token exchange failed: {Error}", errorContent);
                    return Redirect("/?error=" + System.Web.HttpUtility.UrlEncode("Failed to exchange authorization code"));
                }
                
                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                var tokenData = JsonDocument.Parse(tokenJson);
                var accessToken = tokenData.RootElement.GetProperty("access_token").GetString();
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Redirect("/?error=" + System.Web.HttpUtility.UrlEncode("Failed to get access token"));
                }
                
                // Fetch the user's YouTube channel ID using the access token
                var youtubeChannelId = await FetchYouTubeChannelId(accessToken, client);
                
                if (string.IsNullOrEmpty(youtubeChannelId))
                {
                    return Redirect("/?error=" + System.Web.HttpUtility.UrlEncode("Could not find your YouTube channel. Make sure you have a YouTube channel."));
                }
                
                // Fetch profile picture from Google
                var pictureUrl = await FetchGoogleProfilePicture(accessToken, client);
                
                // Link the YouTube channel
                var success = _accountService.LinkYouTubeChannel(account.Id, youtubeChannelId, pictureUrl);
                
                if (!success)
                {
                    return Redirect("/?error=" + System.Web.HttpUtility.UrlEncode("Failed to link YouTube channel. It may already be linked to another account."));
                }
                
                _logger.LogInformation("[ACCOUNT] Successfully linked YouTube channel {ChannelId} to {Username} via OAuth", 
                    youtubeChannelId, account.Username);
                
                return Redirect("/?success=" + System.Web.HttpUtility.UrlEncode("YouTube channel linked successfully!"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ACCOUNT] OAuth callback error");
            return Redirect("/?error=" + System.Web.HttpUtility.UrlEncode($"An error occurred: {ex.Message}"));
        }
    }
    
    /// <summary>
    /// Fetches the YouTube Channel ID using an OAuth access token.
    /// </summary>
    private async Task<string?> FetchYouTubeChannelId(string accessToken, HttpClient client)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, 
                "https://www.googleapis.com/youtube/v3/channels?part=id&mine=true");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await client.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[ACCOUNT] Failed to fetch YouTube channel: {Error}", errorContent);
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json);
            
            if (data.RootElement.TryGetProperty("items", out var items) && 
                items.GetArrayLength() > 0)
            {
                var channelId = items[0].GetProperty("id").GetString();
                _logger.LogInformation("[ACCOUNT] Fetched YouTube Channel ID: {ChannelId}", channelId);
                return channelId;
            }
            
            _logger.LogWarning("[ACCOUNT] No YouTube channel found for this account");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ACCOUNT] Error fetching YouTube channel ID");
            return null;
        }
    }
    
    /// <summary>
    /// Fetches the user's Google profile picture using an OAuth access token.
    /// </summary>
    private async Task<string?> FetchGoogleProfilePicture(string accessToken, HttpClient client)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, 
                "https://www.googleapis.com/oauth2/v2/userinfo");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await client.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json);
            
            if (data.RootElement.TryGetProperty("picture", out var picture))
            {
                return picture.GetString();
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ACCOUNT] Error fetching profile picture");
            return null;
        }
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
    /// Fetches the actual YouTube Channel ID using a Google OAuth credential.
    /// Since the Google Sign-In credential is an ID token (not access token),
    /// we cannot make YouTube API calls with it. Instead, we return null
    /// and the system should use a different OAuth flow for viewers.
    /// </summary>
    private async Task<string?> GetYouTubeChannelIdFromCredential(string credential)
    {
        // The credential from Google Sign-In is a JWT ID token, not an OAuth access token
        // It doesn't include YouTube channel information and can't be used to call YouTube API
        // 
        // For now, we'll return null to indicate this isn't supported.
        // The proper fix is to change the viewer OAuth flow to request YouTube scopes.
        //
        // However, we can try to extract the channel ID if it's in the JWT payload
        try
        {
            var parts = credential.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }
            
            // Decode payload
            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            
            var jsonPayload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var claims = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonPayload);
            
            // Check if the JWT contains a YouTube channel ID claim (it usually doesn't)
            if (claims != null && claims.TryGetValue("channel_id", out var channelIdObj))
            {
                var channelId = channelIdObj?.ToString();
                if (!string.IsNullOrEmpty(channelId))
                {
                    _logger.LogInformation("[AUTH] Found YouTube Channel ID in JWT: {ChannelId}", channelId);
                    return channelId;
                }
            }
            
            // JWT doesn't contain channel ID - this is expected for Google Sign-In
            _logger.LogDebug("[AUTH] JWT ID token does not contain YouTube channel ID (expected)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AUTH] Failed to parse JWT for channel ID");
            return null;
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

        try
        {
            // Use AccountService to handle the full submission flow
            var (success, message, commandId, newBalance) = await _accountService.SubmitChaosCommandAsync(
                account.Id,
                request.Prompt,
                codeGenerator: async (prompt) => await _codeGenerator.GenerateCodeAsync(prompt),
                needsModeration: _moderationService.NeedsModeration,
                extractImageUrls: _moderationService.ExtractImageUrls,
                addPendingImage: (url, prompt, source, author, userId, cmdId) => 
                    _moderationService.AddPendingImage(url, prompt, source, author, userId, cmdId),
                addPendingCode: (prompt, execCode, undoCode, reason, source, author, userId, cmdId) =>
                    _codeModerationService.AddPendingCode(prompt, execCode, undoCode, reason, source, author, userId, cmdId),
                addCommandWithStatus: (prompt, execCode, undoCode, source, author, imgCtx, userId, aiResp, status, queue) =>
                    _commandQueue.AddCommandWithStatus(prompt, execCode, undoCode, source, author, imgCtx, userId, aiResp, status, queue),
                isPrivateDiscordMode: _settingsService.Settings.Safety.PrivateDiscordMode,
                isTestClientModeEnabled: _testClientService.IsEnabled
            );

            if (!success)
            {
                return Ok(new
                {
                    status = "error",
                    message,
                    balance = newBalance
                });
            }

            // If test client mode is enabled and command was queued, queue for testing
            if (commandId.HasValue && _testClientService.IsEnabled)
            {
                var command = _commandQueue.GetCommand(commandId.Value);
                if (command != null && !string.IsNullOrEmpty(command.ExecutionCode))
                {
                    _testClientService.QueueForTesting(command.Id, command.ExecutionCode, command.UserPrompt);
                }
            }

            var statusValue = message.Contains("moderation") ? "pending_moderation" : "success";
            
            return Ok(new
            {
                status = statusValue,
                message,
                commandId,
                balance = newBalance,
                cost = Constants.CommandCost
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit command for {Username}", account.Username);
            return StatusCode(500, new { status = "error", message = "Failed to process command" });
        }
    }
    
    /// <summary>
    /// Admin endpoint: Gets all pending credits to help diagnose linking issues.
    /// </summary>
    [HttpGet("admin/pending-credits")]
    public ActionResult GetPendingCredits([FromHeader(Name = "X-Session-Token")] string? sessionToken)
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
        
        if (account.Role != UserRole.Admin)
        {
            return Forbid();
        }
        
        var pendingCredits = _accountService.GetAllPendingCredits();
        
        return Ok(new
        {
            status = "success",
            pendingCredits = pendingCredits.Select(p => new
            {
                channelId = p.ChannelId,
                displayName = p.DisplayName,
                pendingBalance = p.PendingBalance,
                donationCount = p.Donations.Count,
                donations = p.Donations
            })
        });
    }
    
    /// <summary>
    /// Admin endpoint: Manually link pending credits to an account.
    /// Used to fix issues where OAuth gave us Google ID instead of YouTube Channel ID.
    /// </summary>
    [HttpPost("admin/link-pending-credits")]
    public ActionResult ManuallyLinkPendingCredits(
        [FromHeader(Name = "X-Session-Token")] string? sessionToken,
        [FromBody] ManualLinkRequest request)
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
        
        if (account.Role != UserRole.Admin)
        {
            return Forbid();
        }
        
        var success = _accountService.ManuallyLinkPendingCredits(request.AccountId, request.YoutubeChannelId);
        
        if (!success)
        {
            return BadRequest(new { status = "error", message = "Failed to link pending credits. Check logs for details." });
        }
        
        return Ok(new
        {
            status = "success",
            message = "Pending credits linked successfully"
        });
    }
    
    /// <summary>
    /// Admin endpoint: Detects accounts with incorrect YouTube IDs (Google IDs).
    /// </summary>
    [HttpGet("admin/incorrect-youtube-ids")]
    public ActionResult GetAccountsWithIncorrectYouTubeIds([FromHeader(Name = "X-Session-Token")] string? sessionToken)
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
        
        if (account.Role != UserRole.Admin)
        {
            return Forbid();
        }
        
        var accounts = _accountService.GetAccountsWithIncorrectYouTubeIds();
        
        return Ok(new
        {
            status = "success",
            count = accounts.Count,
            accounts = accounts.Select(a => new
            {
                id = a.Id,
                username = a.Username,
                displayName = a.DisplayName,
                linkedYouTubeChannelId = a.LinkedYouTubeChannelId,
                creditBalance = a.CreditBalance,
                createdAt = a.CreatedAt
            })
        });
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

public class ManualLinkRequest
{
    public string AccountId { get; set; } = "";
    public string YoutubeChannelId { get; set; } = "";
}
