using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Interface for managing application settings persistence.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current settings.
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    void SaveSettings();

    /// <summary>
    /// Updates OpenRouter settings.
    /// </summary>
    void UpdateOpenRouter(string apiKey, string? model = null);

    /// <summary>
    /// Updates General settings.
    /// </summary>
    void UpdateGeneralSettings(bool streamMode);

    /// <summary>
    /// Updates General settings including link blocking.
    /// </summary>
    void UpdateGeneralSettings(bool streamMode, bool blockLinksInGeneratedCode);

    /// <summary>
    /// Updates Twitch settings.
    /// </summary>
    void UpdateTwitch(TwitchSettings twitch);

    /// <summary>
    /// Updates YouTube settings.
    /// </summary>
    void UpdateYouTube(YouTubeSettings youtube);

    /// <summary>
    /// Sets the admin password.
    /// </summary>
    void SetAdminPassword(string password);

    /// <summary>
    /// Validates the admin password.
    /// </summary>
    bool ValidateAdminPassword(string password);

    /// <summary>
    /// Updates tunnel settings.
    /// </summary>
    void UpdateTunnel(TunnelSettings tunnel);

    /// <summary>
    /// Checks if OpenRouter is configured.
    /// </summary>
    bool IsOpenRouterConfigured { get; }

    /// <summary>
    /// Checks if Twitch is configured.
    /// </summary>
    bool IsTwitchConfigured { get; }

    /// <summary>
    /// Checks if YouTube is configured.
    /// </summary>
    bool IsYouTubeConfigured { get; }

    /// <summary>
    /// Checks if admin password is configured.
    /// </summary>
    bool IsAdminConfigured { get; }

    /// <summary>
    /// Updates safety settings.
    /// </summary>
    void UpdateSafetySettings(SafetySettings safety);

    /// <summary>
    /// Toggles Private Discord Mode on/off.
    /// </summary>
    void SetPrivateDiscordMode(bool enabled);

    /// <summary>
    /// Updates test client settings.
    /// </summary>
    void UpdateTestClient(TestClientSettings testClient);

    /// <summary>
    /// Enables or disables test client mode.
    /// </summary>
    void SetTestClientMode(bool enabled);

    /// <summary>
    /// Updates test client connection status.
    /// </summary>
    void UpdateTestClientConnection(bool isConnected);

    /// <summary>
    /// Checks if test client mode is enabled.
    /// </summary>
    bool IsTestClientModeEnabled { get; }

    /// <summary>
    /// Updates YouTube credentials without replacing the entire settings object.
    /// </summary>
    void UpdateYouTubeCredentials(string clientId, string clientSecret, string videoId, decimal minAmount, bool allowChat, bool allowViewerOAuth = true);

    /// <summary>
    /// Updates the YouTube polling interval (in seconds).
    /// </summary>
    void UpdateYouTubePollingInterval(int intervalSeconds);

    /// <summary>
    /// Updates the stream state for persistence.
    /// Called periodically to track if stream was live when app closes.
    /// </summary>
    void UpdateStreamState(bool wasStreamLive, bool wasYouTubeListening, bool wasTwitchListening, 
        string? lastYouTubeVideoId = null, string? lastTwitchChannel = null);

    /// <summary>
    /// Gets the persisted stream state for auto-reconnect.
    /// </summary>
    StreamStateSettings GetStreamState();

    /// <summary>
    /// Clears the stream state (e.g., when manually stopping the stream).
    /// </summary>
    void ClearStreamState();
}
