using System.Text.Json;
using System.Web;
using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChaos.Brain.Controllers;

/// <summary>
/// Controller for setup and OAuth endpoints.
/// </summary>
[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly SettingsService _settingsService;
    private readonly TwitchService _twitchService;
    private readonly YouTubeService _youtubeService;
    private readonly TunnelService _tunnelService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SetupController> _logger;
    
    public SetupController(
        SettingsService settingsService,
        TwitchService twitchService,
        YouTubeService youtubeService,
        TunnelService tunnelService,
        IHttpClientFactory httpClientFactory,
        ILogger<SetupController> logger)
    {
        _settingsService = settingsService;
        _twitchService = twitchService;
        _youtubeService = youtubeService;
        _tunnelService = tunnelService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    
    // ==========================================
    // ADMIN AUTHENTICATION
    // ==========================================
    
    /// <summary>
    /// Check if admin is configured and validate password.
    /// </summary>
    [HttpPost("admin/login")]
    public ActionResult AdminLogin([FromBody] AdminLoginRequest request)
    {
        if (!_settingsService.IsAdminConfigured)
        {
            return Ok(new { status = "not_configured", message = "Admin password not set. Please set one first." });
        }
        
        if (_settingsService.ValidateAdminPassword(request.Password))
        {
            return Ok(new { status = "success", message = "Login successful" });
        }
        
        return Unauthorized(new { status = "error", message = "Invalid password" });
    }
    
    /// <summary>
    /// Set or update admin password.
    /// </summary>
    [HttpPost("admin/password")]
    public ActionResult SetAdminPassword([FromBody] SetPasswordRequest request)
    {
        // If password is already set, require current password
        if (_settingsService.IsAdminConfigured)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) || 
                !_settingsService.ValidateAdminPassword(request.CurrentPassword))
            {
                return Unauthorized(new { status = "error", message = "Current password is incorrect" });
            }
        }
        
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 4)
        {
            return BadRequest(new { status = "error", message = "Password must be at least 4 characters" });
        }
        
        _settingsService.SetAdminPassword(request.NewPassword);
        _logger.LogInformation("Admin password updated");
        
        return Ok(new { status = "success", message = "Password set successfully" });
    }
    
    /// <summary>
    /// Check admin configuration status.
    /// </summary>
    [HttpGet("admin/status")]
    public ActionResult GetAdminStatus()
    {
        return Ok(new { 
            isConfigured = _settingsService.IsAdminConfigured 
        });
    }
    
    // ==========================================
    // TUNNEL MANAGEMENT
    // ==========================================
    
    /// <summary>
    /// Get current tunnel status.
    /// </summary>
    [HttpGet("tunnel/status")]
    public ActionResult GetTunnelStatus()
    {
        var status = _tunnelService.GetStatus();
        return Ok(status);
    }
    
    /// <summary>
    /// Start ngrok tunnel.
    /// </summary>
    [HttpPost("tunnel/ngrok/start")]
    public async Task<ActionResult> StartNgrok()
    {
        var (success, url, error) = await _tunnelService.StartNgrokAsync();
        
        if (success)
        {
            return Ok(new { 
                status = "success", 
                message = "ngrok started", 
                url,
                publicIp = _tunnelService.PublicIp
            });
        }
        
        return BadRequest(new { status = "error", message = error });
    }
    
    /// <summary>
    /// Start localtunnel.
    /// </summary>
    [HttpPost("tunnel/localtunnel/start")]
    public async Task<ActionResult> StartLocalTunnel()
    {
        var (success, url, error) = await _tunnelService.StartLocalTunnelAsync();
        
        if (success)
        {
            return Ok(new { 
                status = "success", 
                message = "localtunnel started", 
                url,
                publicIp = _tunnelService.PublicIp,
                note = "LocalTunnel requires entering your public IP as password on first visit"
            });
        }
        
        return BadRequest(new { status = "error", message = error });
    }
    
    /// <summary>
    /// Start Bore tunnel (bore.pub).
    /// </summary>
    [HttpPost("tunnel/bore/start")]
    public async Task<ActionResult> StartBore()
    {
        var (success, url, error) = await _tunnelService.StartBoreAsync();
        
        if (success)
        {
            return Ok(new { 
                status = "success", 
                message = "bore started", 
                url,
                note = "Bore provides direct access - no account or password required!"
            });
        }
        
        return BadRequest(new { status = "error", message = error });
    }
    
    /// <summary>
    /// Stop current tunnel.
    /// </summary>
    [HttpPost("tunnel/stop")]
    public ActionResult StopTunnel()
    {
        _tunnelService.Stop();
        return Ok(new { status = "success", message = "Tunnel stopped" });
    }
    
    // ==========================================
    // STATUS
    // ==========================================
    
    /// <summary>
    /// Gets the current setup status.
    /// </summary>
    [HttpGet("status")]
    public ActionResult<SetupStatus> GetStatus()
    {
        var settings = _settingsService.Settings;
        var tunnelStatus = _tunnelService.GetStatus();
        
        return Ok(new SetupStatus
        {
            OpenRouterConfigured = _settingsService.IsOpenRouterConfigured,
            AdminConfigured = _settingsService.IsAdminConfigured,
            CurrentModel = settings.OpenRouter.Model,
            Twitch = new TwitchAuthState
            {
                IsAuthenticated = _settingsService.IsTwitchConfigured,
                Channel = settings.Twitch.Channel,
                IsListening = _twitchService.IsConnected
            },
            YouTube = new YouTubeAuthState
            {
                IsAuthenticated = _settingsService.IsYouTubeConfigured,
                VideoId = settings.YouTube.VideoId,
                IsListening = _youtubeService.IsListening
            },
            Tunnel = new TunnelState
            {
                IsRunning = tunnelStatus.IsRunning,
                Type = tunnelStatus.Type.ToString(),
                Url = tunnelStatus.Url,
                PublicIp = tunnelStatus.PublicIp
            }
        });
    }
    
    /// <summary>
    /// Gets available AI models.
    /// </summary>
    [HttpGet("models")]
    public ActionResult GetModels()
    {
        var models = new[]
        {
            new { id = "anthropic/claude-sonnet-4.5", name = "Claude Sonnet 4.5 (Recommended)", provider = "Anthropic" },
            new { id = "anthropic/claude-3.5-sonnet", name = "Claude 3.5 Sonnet", provider = "Anthropic" },
            new { id = "anthropic/claude-3-opus", name = "Claude 3 Opus", provider = "Anthropic" },
            new { id = "openai/gpt-4o", name = "GPT-4o", provider = "OpenAI" },
            new { id = "openai/gpt-4-turbo", name = "GPT-4 Turbo", provider = "OpenAI" },
            new { id = "openai/gpt-4", name = "GPT-4", provider = "OpenAI" },
            new { id = "google/gemini-pro-1.5", name = "Gemini Pro 1.5", provider = "Google" },
            new { id = "google/gemini-2.5-flash", name = "Gemini Flash 2.5", provider = "Google" },
            new { id = "google/gemini-flash-1.5", name = "Gemini Flash 1.5", provider = "Google" },
            new { id = "meta-llama/llama-3.1-70b-instruct", name = "Llama 3.1 70B", provider = "Meta" },
            new { id = "mistralai/mixtral-8x22b-instruct", name = "Mixtral 8x22B", provider = "Mistral" },
            new { id = "x-ai/grok-code-fast-1", name = "Grok Code Fast 1", provider = "xAI" },
            new { id = "x-ai/grok-4.1-fast:free", name = "Grok 4.1 Fast (Free)", provider = "xAI" }
        };
        
        return Ok(new { 
            models, 
            currentModel = _settingsService.Settings.OpenRouter.Model 
        });
    }
    
    /// <summary>
    /// Gets the current OpenRouter settings.
    /// </summary>
    [HttpGet("openrouter")]
    public ActionResult GetOpenRouter()
    {
        var settings = _settingsService.Settings.OpenRouter;
        
        // Mask the API key for display, but show enough to identify it
        string? maskedKey = null;
        if (!string.IsNullOrEmpty(settings.ApiKey))
        {
            if (settings.ApiKey.Length > 12)
            {
                maskedKey = settings.ApiKey[..8] + "..." + settings.ApiKey[^4..];
            }
            else
            {
                maskedKey = "****";
            }
        }
        
        return Ok(new { 
            apiKey = settings.ApiKey, // Return the full key so it can be used
            maskedKey,
            model = settings.Model,
            isConfigured = _settingsService.IsOpenRouterConfigured
        });
    }
    
    /// <summary>
    /// Saves the OpenRouter API key.
    /// </summary>
    [HttpPost("openrouter")]
    public ActionResult SaveOpenRouter([FromBody] OpenRouterSettings settings)
    {
        _settingsService.UpdateOpenRouter(settings.ApiKey, settings.Model);
        _logger.LogInformation("OpenRouter API key saved");
        
        return Ok(new { status = "success", message = "OpenRouter settings saved" });
    }
    
    // ==========================================
    // TWITCH OAUTH
    // ==========================================
    
    /// <summary>
    /// Saves Twitch client credentials.
    /// </summary>
    [HttpPost("twitch/credentials")]
    public ActionResult SaveTwitchCredentials([FromBody] TwitchSettings settings)
    {
        var existing = _settingsService.Settings.Twitch;
        existing.ClientId = settings.ClientId;
        existing.ClientSecret = settings.ClientSecret;
        existing.Channel = settings.Channel;
        existing.ChatCommand = settings.ChatCommand;
        existing.RequireBits = settings.RequireBits;
        existing.MinBitsAmount = settings.MinBitsAmount;
        existing.CooldownSeconds = settings.CooldownSeconds;
        
        _settingsService.UpdateTwitch(existing);
        _logger.LogInformation("Twitch credentials saved");
        
        return Ok(new { status = "success", message = "Twitch settings saved" });
    }
    
    /// <summary>
    /// Gets the Twitch OAuth authorization URL.
    /// </summary>
    [HttpGet("twitch/auth-url")]
    public ActionResult GetTwitchAuthUrl([FromQuery] string? redirectUri = null)
    {
        var settings = _settingsService.Settings.Twitch;
        
        if (string.IsNullOrEmpty(settings.ClientId))
        {
            return BadRequest(new { status = "error", message = "Twitch Client ID not configured" });
        }
        
        redirectUri ??= $"{Request.Scheme}://{Request.Host}/api/setup/twitch/callback";
        
        var scopes = "chat:read chat:edit bits:read channel:read:subscriptions";
        var authUrl = $"https://id.twitch.tv/oauth2/authorize" +
            $"?client_id={settings.ClientId}" +
            $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={HttpUtility.UrlEncode(scopes)}";
        
        return Ok(new { authUrl, redirectUri });
    }
    
    /// <summary>
    /// Handles the Twitch OAuth callback.
    /// </summary>
    [HttpGet("twitch/callback")]
    public async Task<ActionResult> TwitchCallback([FromQuery] string? code, [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return Redirect($"/setup?error=twitch_denied");
        }
        
        if (string.IsNullOrEmpty(code))
        {
            return Redirect($"/setup?error=twitch_no_code");
        }
        
        var settings = _settingsService.Settings.Twitch;
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/setup/twitch/callback";
        
        try
        {
            var client = _httpClientFactory.CreateClient();
            var tokenResponse = await client.PostAsync(
                "https://id.twitch.tv/oauth2/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = settings.ClientId,
                    ["client_secret"] = settings.ClientSecret,
                    ["code"] = code,
                    ["grant_type"] = "authorization_code",
                    ["redirect_uri"] = redirectUri
                }));
            
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogError("Twitch token exchange failed: {Error}", errorContent);
                return Redirect($"/setup?error=twitch_token_failed");
            }
            
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonDocument.Parse(tokenJson);
            
            settings.AccessToken = tokenData.RootElement.GetProperty("access_token").GetString() ?? "";
            settings.RefreshToken = tokenData.RootElement.TryGetProperty("refresh_token", out var rt) 
                ? rt.GetString() ?? "" 
                : "";
            
            // Get username
            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
            userRequest.Headers.Add("Authorization", $"Bearer {settings.AccessToken}");
            userRequest.Headers.Add("Client-Id", settings.ClientId);
            
            var userResponse = await client.SendAsync(userRequest);
            if (userResponse.IsSuccessStatusCode)
            {
                var userJson = await userResponse.Content.ReadAsStringAsync();
                var userData = JsonDocument.Parse(userJson);
                var users = userData.RootElement.GetProperty("data");
                if (users.GetArrayLength() > 0)
                {
                    var username = users[0].GetProperty("login").GetString();
                    if (string.IsNullOrEmpty(settings.Channel))
                    {
                        settings.Channel = username ?? "";
                    }
                }
            }
            
            _settingsService.UpdateTwitch(settings);
            _logger.LogInformation("Twitch OAuth successful");
            
            return Redirect("/setup?success=twitch");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twitch OAuth callback error");
            return Redirect($"/setup?error=twitch_exception");
        }
    }
    
    /// <summary>
    /// Connects to Twitch chat.
    /// </summary>
    [HttpPost("twitch/connect")]
    public async Task<ActionResult> ConnectTwitch()
    {
        var success = await _twitchService.ConnectAsync();
        
        if (success)
        {
            return Ok(new { status = "success", message = "Connected to Twitch", channel = _twitchService.ConnectedChannel });
        }
        
        return BadRequest(new { status = "error", message = "Failed to connect to Twitch" });
    }
    
    /// <summary>
    /// Disconnects from Twitch chat.
    /// </summary>
    [HttpPost("twitch/disconnect")]
    public ActionResult DisconnectTwitch()
    {
        _twitchService.Disconnect();
        return Ok(new { status = "success", message = "Disconnected from Twitch" });
    }
    
    // ==========================================
    // YOUTUBE OAUTH
    // ==========================================
    
    /// <summary>
    /// Saves YouTube client credentials.
    /// </summary>
    [HttpPost("youtube/credentials")]
    public ActionResult SaveYouTubeCredentials([FromBody] YouTubeSettings settings)
    {
        var existing = _settingsService.Settings.YouTube;
        existing.ClientId = settings.ClientId;
        existing.ClientSecret = settings.ClientSecret;
        existing.VideoId = settings.VideoId;
        existing.ChatCommand = settings.ChatCommand;
        existing.AllowRegularChat = settings.AllowRegularChat;
        existing.MinSuperChatAmount = settings.MinSuperChatAmount;
        existing.CooldownSeconds = settings.CooldownSeconds;
        
        _settingsService.UpdateYouTube(existing);
        _logger.LogInformation("YouTube credentials saved");
        
        return Ok(new { status = "success", message = "YouTube settings saved" });
    }
    
    /// <summary>
    /// Gets the YouTube OAuth authorization URL.
    /// </summary>
    [HttpGet("youtube/auth-url")]
    public ActionResult GetYouTubeAuthUrl([FromQuery] string? redirectUri = null)
    {
        var settings = _settingsService.Settings.YouTube;
        
        if (string.IsNullOrEmpty(settings.ClientId))
        {
            return BadRequest(new { status = "error", message = "YouTube Client ID not configured" });
        }
        
        redirectUri ??= $"{Request.Scheme}://{Request.Host}/api/setup/youtube/callback";
        
        var scopes = "https://www.googleapis.com/auth/youtube.readonly";
        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={settings.ClientId}" +
            $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={HttpUtility.UrlEncode(scopes)}" +
            $"&access_type=offline" +
            $"&prompt=consent";
        
        return Ok(new { authUrl, redirectUri });
    }
    
    /// <summary>
    /// Handles the YouTube OAuth callback.
    /// </summary>
    [HttpGet("youtube/callback")]
    public async Task<ActionResult> YouTubeCallback([FromQuery] string? code, [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return Redirect($"/setup?error=youtube_denied");
        }
        
        if (string.IsNullOrEmpty(code))
        {
            return Redirect($"/setup?error=youtube_no_code");
        }
        
        var settings = _settingsService.Settings.YouTube;
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/setup/youtube/callback";
        
        try
        {
            var client = _httpClientFactory.CreateClient();
            var tokenResponse = await client.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = settings.ClientId,
                    ["client_secret"] = settings.ClientSecret,
                    ["code"] = code,
                    ["grant_type"] = "authorization_code",
                    ["redirect_uri"] = redirectUri
                }));
            
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogError("YouTube token exchange failed: {Error}", errorContent);
                return Redirect($"/setup?error=youtube_token_failed");
            }
            
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonDocument.Parse(tokenJson);
            
            settings.AccessToken = tokenData.RootElement.GetProperty("access_token").GetString() ?? "";
            settings.RefreshToken = tokenData.RootElement.TryGetProperty("refresh_token", out var rt)
                ? rt.GetString() ?? ""
                : "";
            
            _settingsService.UpdateYouTube(settings);
            _logger.LogInformation("YouTube OAuth successful");
            
            return Redirect("/setup?success=youtube");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YouTube OAuth callback error");
            return Redirect($"/setup?error=youtube_exception");
        }
    }
    
    /// <summary>
    /// Starts listening to YouTube live chat.
    /// </summary>
    [HttpPost("youtube/start")]
    public async Task<ActionResult> StartYouTube([FromBody] YouTubeSettings? settings = null)
    {
        var videoId = settings?.VideoId ?? _settingsService.Settings.YouTube.VideoId;
        var success = await _youtubeService.StartListeningAsync(videoId);
        
        if (success)
        {
            return Ok(new { status = "success", message = "Started listening to YouTube", videoId = _youtubeService.CurrentVideoId });
        }
        
        return BadRequest(new { status = "error", message = "Failed to start YouTube listener. Make sure the video is a live stream with an active chat." });
    }
    
    /// <summary>
    /// Stops listening to YouTube live chat.
    /// </summary>
    [HttpPost("youtube/stop")]
    public ActionResult StopYouTube()
    {
        _youtubeService.StopListening();
        return Ok(new { status = "success", message = "Stopped listening to YouTube" });
    }
}

// Request models
public class AdminLoginRequest
{
    public string Password { get; set; } = "";
}

public class SetPasswordRequest
{
    public string? CurrentPassword { get; set; }
    public string NewPassword { get; set; } = "";
}
