using System.Text.Json;
using AIChaos.Brain.Models;
using Microsoft.Extensions.Options;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing application settings persistence.
/// </summary>
public class SettingsService
{
    private readonly string _settingsPath;
    private readonly ILogger<SettingsService> _logger;
    private AppSettings _settings;
    private readonly object _lock = new();
    
    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
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
    /// Loads settings from disk or returns defaults.
    /// </summary>
    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (settings != null)
                {
                    _logger.LogInformation("Settings loaded from {Path}", _settingsPath);
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, using defaults");
        }
        
        return new AppSettings();
    }
    
    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    public void SaveSettings()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_settingsPath, json);
                _logger.LogInformation("Settings saved to {Path}", _settingsPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
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
}
