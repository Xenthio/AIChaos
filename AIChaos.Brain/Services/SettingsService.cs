using System.Text.Json;
using AIChaos.Brain.Models;
using AIChaos.Brain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing application settings persistence using Entity Framework Core.
/// Uses IDbContextFactory for thread-safe database access from singleton service.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<AIChaosDbContext> _dbContextFactory;
    private readonly ILogger<SettingsService> _logger;
    private AppSettings _settings;
    private readonly object _lock = new();
    
    // Settings is a singleton row in the database
    private const int SINGLETON_SETTINGS_ID = 1;
    
    public SettingsService(
        IDbContextFactory<AIChaosDbContext> dbContextFactory,
        ILogger<SettingsService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _settings = LoadSettings();
    }
    
    public AppSettings Settings
    {
        get
        {
            lock (_lock)
            {
                return _settings;
            }
        }
    }
    
    /// <summary>
    /// Loads settings from database or returns defaults.
    /// </summary>
    private AppSettings LoadSettings()
    {
        try
        {
            using var context = _dbContextFactory.CreateDbContext();
            var settings = context.Settings.FirstOrDefault();
            
            if (settings != null)
            {
                _logger.LogInformation("Settings loaded from database");
                return settings;
            }
            
            _logger.LogInformation("No settings found in database, creating defaults");
            var newSettings = new AppSettings { Id = SINGLETON_SETTINGS_ID };
            context.Settings.Add(newSettings);
            context.SaveChanges();
            return newSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from database, using defaults");
            return new AppSettings { Id = SINGLETON_SETTINGS_ID };
        }
    }
    
    /// <summary>
    /// Saves current settings to database using Update for proper upsert.
    /// </summary>
    public void SaveSettings()
    {
        lock (_lock)
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                // Use Update which handles both insert and update scenarios
                context.Settings.Update(_settings);
                context.SaveChanges();
                _logger.LogInformation("Settings saved to database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings to database");
            }
        }
    }
    
    /// <summary>
    /// Updates OpenRouter settings.
    /// </summary>
    public void UpdateOpenRouter(string apiKey, string? model = null)
    {
        lock (_lock)
        {
            _settings.OpenRouter.ApiKey = apiKey;
            if (!string.IsNullOrEmpty(model))
            {
                _settings.OpenRouter.Model = model;
            }
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Updates General settings.
    /// </summary>
    public void UpdateGeneralSettings(bool streamMode)
    {
        lock (_lock)
        {
            _settings.General.StreamMode = streamMode;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Updates General settings including link blocking.
    /// </summary>
    public void UpdateGeneralSettings(bool streamMode, bool blockLinksInGeneratedCode)
    {
        lock (_lock)
        {
            _settings.General.StreamMode = streamMode;
            _settings.General.BlockLinksInGeneratedCode = blockLinksInGeneratedCode;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Updates Twitch settings.
    /// </summary>
    public void UpdateTwitch(TwitchSettings twitch)
    {
        lock (_lock)
        {
            _settings.Twitch = twitch;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Updates YouTube settings.
    /// </summary>
    public void UpdateYouTube(YouTubeSettings youtube)
    {
        lock (_lock)
        {
            _settings.YouTube = youtube;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Sets the admin password.
    /// </summary>
    public void SetAdminPassword(string password)
    {
        lock (_lock)
        {
            _settings.Admin.Password = password;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Validates the admin password.
    /// </summary>
    public bool ValidateAdminPassword(string password)
    {
        lock (_lock)
        {
            // If no password is set, deny access
            if (!_settings.Admin.IsConfigured)
            {
                return false;
            }
            return _settings.Admin.Password == password;
        }
    }
    
    /// <summary>
    /// Updates tunnel settings.
    /// </summary>
    public void UpdateTunnel(TunnelSettings tunnel)
    {
        lock (_lock)
        {
            _settings.Tunnel = tunnel;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Checks if OpenRouter is configured.
    /// </summary>
    public bool IsOpenRouterConfigured => !string.IsNullOrEmpty(_settings.OpenRouter.ApiKey);
    
    /// <summary>
    /// Checks if Twitch is configured.
    /// </summary>
    public bool IsTwitchConfigured => 
        !string.IsNullOrEmpty(_settings.Twitch.ClientId) && 
        !string.IsNullOrEmpty(_settings.Twitch.AccessToken);
    
    /// <summary>
    /// Checks if YouTube is configured.
    /// </summary>
    public bool IsYouTubeConfigured => 
        !string.IsNullOrEmpty(_settings.YouTube.ClientId) && 
        !string.IsNullOrEmpty(_settings.YouTube.AccessToken);
    
    /// <summary>
    /// Checks if admin password is configured.
    /// </summary>
    public bool IsAdminConfigured => _settings.Admin.IsConfigured;
    
    /// <summary>
    /// Updates safety settings.
    /// </summary>
    public void UpdateSafetySettings(SafetySettings safety)
    {
        lock (_lock)
        {
            _settings.Safety = safety;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Toggles Private Discord Mode on/off.
    /// </summary>
    public void SetPrivateDiscordMode(bool enabled)
    {
        lock (_lock)
        {
            _settings.Safety.PrivateDiscordMode = enabled;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Updates test client settings.
    /// </summary>
    public void UpdateTestClient(TestClientSettings testClient)
    {
        lock (_lock)
        {
            _settings.TestClient = testClient;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Enables or disables test client mode.
    /// </summary>
    public void SetTestClientMode(bool enabled)
    {
        lock (_lock)
        {
            _settings.TestClient.Enabled = enabled;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Updates test client connection status.
    /// </summary>
    public void UpdateTestClientConnection(bool isConnected)
    {
        lock (_lock)
        {
            _settings.TestClient.IsConnected = isConnected;
            if (isConnected)
            {
                _settings.TestClient.LastPollTime = DateTime.UtcNow;
            }
            // Don't save to disk - this is runtime state
        }
    }
    
    /// <summary>
    /// Checks if test client mode is enabled.
    /// </summary>
    public bool IsTestClientModeEnabled => _settings.TestClient.Enabled;
    
    /// <summary>
    /// Updates YouTube credentials without replacing the entire settings object.
    /// </summary>
    public void UpdateYouTubeCredentials(string clientId, string clientSecret, string videoId, decimal minAmount, bool allowChat, bool allowViewerOAuth = true)
    {
        lock (_lock)
        {
            _settings.YouTube.ClientId = clientId;
            _settings.YouTube.ClientSecret = clientSecret;
            _settings.YouTube.VideoId = videoId;
            _settings.YouTube.MinSuperChatAmount = minAmount;
            _settings.YouTube.AllowRegularChat = allowChat;
            _settings.YouTube.AllowViewerOAuth = allowViewerOAuth;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Updates the YouTube polling interval (in seconds).
    /// </summary>
    public void UpdateYouTubePollingInterval(int intervalSeconds)
    {
        lock (_lock)
        {
            // Enforce minimum of 1 second to prevent abuse
            _settings.YouTube.PollingIntervalSeconds = Math.Max(1, intervalSeconds);
            SaveSettings();
            _logger.LogInformation("[Settings] YouTube polling interval updated to {Interval} seconds", _settings.YouTube.PollingIntervalSeconds);
        }
    }
    
    /// <summary>
    /// Updates the stream state for persistence.
    /// Called periodically to track if stream was live when app closes.
    /// </summary>
    public void UpdateStreamState(bool wasStreamLive, bool wasYouTubeListening, bool wasTwitchListening, 
        string? lastYouTubeVideoId = null, string? lastTwitchChannel = null)
    {
        lock (_lock)
        {
            _settings.StreamState.WasStreamLive = wasStreamLive;
            _settings.StreamState.WasYouTubeListening = wasYouTubeListening;
            _settings.StreamState.WasTwitchListening = wasTwitchListening;
            _settings.StreamState.LastYouTubeVideoId = lastYouTubeVideoId ?? _settings.StreamState.LastYouTubeVideoId;
            _settings.StreamState.LastTwitchChannel = lastTwitchChannel ?? _settings.StreamState.LastTwitchChannel;
            _settings.StreamState.LastUpdated = DateTime.UtcNow;
            SaveSettings();
        }
    }
    
    /// <summary>
    /// Gets the persisted stream state for auto-reconnect.
    /// </summary>
    public StreamStateSettings GetStreamState()
    {
        lock (_lock)
        {
            return _settings.StreamState;
        }
    }
    
    /// <summary>
    /// Clears the stream state (e.g., when manually stopping the stream).
    /// </summary>
    public void ClearStreamState()
    {
        lock (_lock)
        {
            _settings.StreamState = new StreamStateSettings();
            SaveSettings();
        }
    }
}
