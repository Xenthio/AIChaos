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
    // MODERATION AUTHENTICATION
    // ==========================================
    
    /// <summary>
    /// Validate moderation password.
    /// </summary>
    [HttpPost("moderation/login")]
    public ActionResult ModerationLogin([FromBody] ModerationLoginRequest request)
    {
        if (_settingsService.ValidateModerationPassword(request.Password))
        {
            return Ok(new { status = "success", message = "Login successful" });
        }
        
        return Unauthorized(new { status = "error", message = "Invalid moderation password" });
    }
    
    // ==========================================
    // PRIVATE DISCORD MODE
    // ==========================================
    
    /// <summary>
    /// Get Private Discord Mode status.
    /// </summary>
    [HttpGet("private-discord-mode")]
    public ActionResult GetPrivateDiscordMode()
    {
        return Ok(new { 
            enabled = _settingsService.Settings.Safety.PrivateDiscordMode 
        });
    }
    
    /// <summary>
    /// Set Private Discord Mode (disables all safety filters in prompt).
    /// </summary>
    [HttpPost("private-discord-mode")]
    public ActionResult SetPrivateDiscordMode([FromBody] PrivateDiscordModeRequest request)
    {
        _settingsService.SetPrivateDiscordMode(request.Enabled);
        _logger.LogInformation("Private Discord Mode set to: {Enabled}", request.Enabled);
        
        return Ok(new { 
            status = "success", 
            message = request.Enabled ? "Private Discord Mode enabled - all safety filters disabled" : "Private Discord Mode disabled",
            enabled = request.Enabled
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
            },
            TestClient = new TestClientState
            {
                Enabled = settings.TestClient.Enabled,
                IsConnected = settings.TestClient.IsConnected,
                TestMap = settings.TestClient.TestMap,
                CleanupAfterTest = settings.TestClient.CleanupAfterTest,
                TimeoutSeconds = settings.TestClient.TimeoutSeconds,
                GmodPath = settings.TestClient.GmodPath,
                LastPollTime = settings.TestClient.LastPollTime
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
            new { id = "anthropic/claude-haiku-4.5", name = "Claude Haiku 4.5", provider = "Anthropic" },
            new { id = "anthropic/claude-opus-4.5", name = "Claude Opus 4.5", provider = "Anthropic" },
            new { id = "anthropic/claude-3.5-sonnet", name = "Claude 3.5 Sonnet", provider = "Anthropic" },
            new { id = "openai/gpt-5.1-codex-mini", name = "GPT-5.1 Codex Mini", provider = "OpenAI" },
            new { id = "google/gemini-3-pro-preview", name = "Gemini 3 Pro Preview", provider = "Google" },
            new { id = "google/gemini-2.5-flash", name = "Gemini Flash 2.5", provider = "Google" },
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
    // AI PROVIDER CONFIGURATION
    // ==========================================
    
    /// <summary>
    /// Gets the current AI provider configuration.
    /// </summary>
    [HttpGet("ai-provider")]
    public ActionResult GetAiProvider()
    {
        var settings = _settingsService.Settings;
        return Ok(new
        {
            provider = settings.Ai.Provider.ToString(),
            openRouter = new
            {
                baseUrl = settings.OpenRouter.BaseUrl,
                model = settings.OpenRouter.Model,
                hasApiKey = !string.IsNullOrEmpty(settings.OpenRouter.ApiKey)
            },
            ollama = new
            {
                baseUrl = settings.Ollama.BaseUrl,
                model = settings.Ollama.Model
            },
            oobabooga = new
            {
                baseUrl = settings.Oobabooga.BaseUrl,
                model = settings.Oobabooga.Model,
                hasApiKey = !string.IsNullOrEmpty(settings.Oobabooga.ApiKey)
            }
        });
    }
    
    /// <summary>
    /// Sets the AI provider (OpenRouter, Ollama, or Oobabooga).
    /// </summary>
    [HttpPost("ai-provider")]
    public ActionResult SetAiProvider([FromBody] AiProviderRequest request)
    {
        if (!Enum.TryParse<AiProvider>(request.Provider, ignoreCase: true, out var provider))
        {
            return BadRequest(new { status = "error", message = "Invalid provider. Must be OpenRouter, Ollama, or Oobabooga." });
        }
        
        _settingsService.SetAiProvider(provider);
        _logger.LogInformation("AI provider set to: {Provider}", provider);
        
        return Ok(new { status = "success", message = $"AI provider set to {provider}", provider = provider.ToString() });
    }
    
    /// <summary>
    /// Updates Ollama settings.
    /// </summary>
    [HttpPost("ollama")]
    public ActionResult SaveOllamaSettings([FromBody] OllamaSettings settings)
    {
        _settingsService.UpdateOllama(settings);
        _logger.LogInformation("Ollama settings saved");
        
        return Ok(new { status = "success", message = "Ollama settings saved" });
    }
    
    /// <summary>
    /// Gets Ollama settings.
    /// </summary>
    [HttpGet("ollama")]
    public ActionResult GetOllamaSettings()
    {
        var settings = _settingsService.Settings.Ollama;
        return Ok(new
        {
            baseUrl = settings.BaseUrl,
            model = settings.Model
        });
    }
    
    /// <summary>
    /// Updates Oobabooga (text-generation-webui) settings.
    /// </summary>
    [HttpPost("oobabooga")]
    public ActionResult SaveOobaboogaSettings([FromBody] OobaSettings settings)
    {
        _settingsService.UpdateOobabooga(settings);
        _logger.LogInformation("Oobabooga settings saved");
        
        return Ok(new { status = "success", message = "Oobabooga settings saved" });
    }
    
    /// <summary>
    /// Gets Oobabooga settings.
    /// </summary>
    [HttpGet("oobabooga")]
    public ActionResult GetOobaboogaSettings()
    {
        var settings = _settingsService.Settings.Oobabooga;
        return Ok(new
        {
            baseUrl = settings.BaseUrl,
            model = settings.Model,
            hasApiKey = !string.IsNullOrEmpty(settings.ApiKey)
        });
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
    
    // ==========================================
    // TEST CLIENT MODE
    // ==========================================
    
    /// <summary>
    /// Get test client mode status.
    /// </summary>
    [HttpGet("test-client")]
    public ActionResult GetTestClientStatus()
    {
        var settings = _settingsService.Settings.TestClient;
        return Ok(new { 
            enabled = settings.Enabled,
            isConnected = settings.IsConnected,
            testMap = settings.TestMap,
            cleanupAfterTest = settings.CleanupAfterTest,
            timeoutSeconds = settings.TimeoutSeconds,
            gmodPath = settings.GmodPath,
            lastPollTime = settings.LastPollTime
        });
    }
    
    /// <summary>
    /// Enable or disable test client mode.
    /// </summary>
    [HttpPost("test-client/toggle")]
    public ActionResult ToggleTestClientMode([FromBody] TestClientModeRequest request)
    {
        _settingsService.SetTestClientMode(request.Enabled);
        _logger.LogInformation("Test Client Mode set to: {Enabled}", request.Enabled);
        
        return Ok(new { 
            status = "success", 
            message = request.Enabled ? "Test Client Mode enabled" : "Test Client Mode disabled",
            enabled = request.Enabled
        });
    }
    
    /// <summary>
    /// Update test client settings.
    /// </summary>
    [HttpPost("test-client/settings")]
    public ActionResult UpdateTestClientSettings([FromBody] TestClientSettingsRequest request)
    {
        var existing = _settingsService.Settings.TestClient;
        existing.TestMap = request.TestMap ?? existing.TestMap;
        existing.CleanupAfterTest = request.CleanupAfterTest ?? existing.CleanupAfterTest;
        existing.TimeoutSeconds = request.TimeoutSeconds ?? existing.TimeoutSeconds;
        existing.GmodPath = request.GmodPath ?? existing.GmodPath;
        
        _settingsService.UpdateTestClient(existing);
        _logger.LogInformation("Test Client settings updated");
        
        return Ok(new { status = "success", message = "Test Client settings saved" });
    }
    
    /// <summary>
    /// Launch a test client instance of GMod with -multirun.
    /// </summary>
    [HttpPost("test-client/launch")]
    public ActionResult LaunchTestClient()
    {
        var settings = _settingsService.Settings.TestClient;
        
        if (string.IsNullOrEmpty(settings.GmodPath))
        {
            return BadRequest(new { status = "error", message = "GMod path not configured. Please set the path to your GMod executable." });
        }
        
        if (!System.IO.File.Exists(settings.GmodPath))
        {
            return BadRequest(new { status = "error", message = $"GMod executable not found at: {settings.GmodPath}" });
        }
        
        // Validate map name to prevent command injection
        // Map names should only contain alphanumeric characters, underscores, and hyphens
        var mapName = settings.TestMap;
        if (string.IsNullOrEmpty(mapName))
        {
            mapName = "gm_flatgrass";
        }
        
        if (!System.Text.RegularExpressions.Regex.IsMatch(mapName, @"^[a-zA-Z0-9_\-]+$"))
        {
            return BadRequest(new { status = "error", message = "Invalid map name. Map names can only contain letters, numbers, underscores, and hyphens." });
        }
        
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = settings.GmodPath,
                Arguments = $"-multirun -windowed -w 640 -h 480 +map {mapName} +ai_chaos_test_client 1",
                UseShellExecute = true
            };
            
            System.Diagnostics.Process.Start(startInfo);
            _logger.LogInformation("Launched test client with map: {Map}", mapName);
            
            return Ok(new { 
                status = "success", 
                message = $"Test client launched with map {mapName}",
                note = "The test client will connect automatically when the game loads."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch test client");
            return BadRequest(new { status = "error", message = $"Failed to launch test client: {ex.Message}" });
        }
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

public class PrivateDiscordModeRequest
{
    public bool Enabled { get; set; } = false;
}

public class ModerationLoginRequest
{
    public string Password { get; set; } = "";
}
public class TestClientModeRequest
{
    public bool Enabled { get; set; } = false;
}

public class TestClientSettingsRequest
{
    public string? TestMap { get; set; }
    public bool? CleanupAfterTest { get; set; }
    public int? TimeoutSeconds { get; set; }
    public string? GmodPath { get; set; }
}

public class AiProviderRequest
{
    public string Provider { get; set; } = "OpenRouter";
}
