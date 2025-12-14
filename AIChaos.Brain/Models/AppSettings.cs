namespace AIChaos.Brain.Models;

/// <summary>
/// Configuration settings for the Chaos Brain application.
/// </summary>
public class AppSettings
{
    public int Id { get; set; } = 1; // Singleton settings row
    public OpenRouterSettings OpenRouter { get; set; } = new();
    public TwitchSettings Twitch { get; set; } = new();
    public YouTubeSettings YouTube { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public AdminSettings Admin { get; set; } = new();
    public TunnelSettings Tunnel { get; set; } = new();
    public TestClientSettings TestClient { get; set; } = new();
    public GeneralSettings General { get; set; } = new();
    public StreamStateSettings StreamState { get; set; } = new();
}

public class OpenRouterSettings
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string Model { get; set; } = "anthropic/claude-sonnet-4.5";
}

public class TwitchSettings
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string Channel { get; set; } = "";
    public bool RequireBits { get; set; } = false;
    public int MinBitsAmount { get; set; } = 100;
    public string ChatCommand { get; set; } = "!chaos";
    public int CooldownSeconds { get; set; } = 5;
    public bool Enabled { get; set; } = false;
}

public class YouTubeSettings
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string VideoId { get; set; } = "";
    public decimal MinSuperChatAmount { get; set; } = 1.00m;
    public bool AllowRegularChat { get; set; } = false;
    public string ChatCommand { get; set; } = "!chaos";
    public int CooldownSeconds { get; set; } = 5;
    public bool Enabled { get; set; } = false;
    public bool AllowViewerOAuth { get; set; } = true; // Allow first 100 viewers to use OAuth login
    public int PollingIntervalSeconds { get; set; } = 20; // Minimum time between polls (default 20 seconds)
}

public class SafetySettings
{
    public bool BlockUrls { get; set; } = true;
    public List<string> AllowedDomains { get; set; } = new() { "i.imgur.com", "imgur.com" };
    public List<string> Moderators { get; set; } = new();
    public bool PrivateDiscordMode { get; set; } = false;
}

public class GeneralSettings
{
    /// <summary>
    /// When enabled, the system runs in stream mode where:
    /// - Login is required for regular users
    /// - Credits are needed for submissions
    /// - Full authentication and credit system is active
    /// When disabled (default), runs in single-user mode where:
    /// - No login is required for regular users
    /// - No credits are needed (unlimited submissions)
    /// - Admin login is still required for dashboard access
    /// </summary>
    public bool StreamMode { get; set; } = false;
    
    /// <summary>
    /// When enabled, all URLs/links found in AI-generated code will be stripped out.
    /// This prevents the AI from generating code that accesses external resources.
    /// </summary>
    public bool BlockLinksInGeneratedCode { get; set; } = true;
}

public class AdminSettings
{
    public string Password { get; set; } = "";
    public bool IsConfigured => !string.IsNullOrEmpty(Password);
}

public class TunnelSettings
{
    public TunnelType Type { get; set; } = TunnelType.None;
    public string CurrentUrl { get; set; } = "";
    public bool IsRunning { get; set; } = false;
}

public enum TunnelType
{
    None,
    Ngrok,
    LocalTunnel,
    Bore
}

/// <summary>
/// Settings for test client mode - runs commands on a separate GMod instance first.
/// </summary>
public class TestClientSettings
{
    /// <summary>
    /// Whether test client mode is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// The map to load on the test client (should be a small/fast-loading map).
    /// </summary>
    public string TestMap { get; set; } = "gm_flatgrass";
    
    /// <summary>
    /// Whether to run gmod_admin_cleanup after each test.
    /// </summary>
    public bool CleanupAfterTest { get; set; } = true;
    
    /// <summary>
    /// Timeout in seconds to wait for test client to respond.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
    
    /// <summary>
    /// Path to the GMod executable for launching test client.
    /// </summary>
    public string GmodPath { get; set; } = "";
    
    /// <summary>
    /// Whether a test client is currently connected.
    /// </summary>
    public bool IsConnected { get; set; } = false;
    
    /// <summary>
    /// Last time the test client polled.
    /// </summary>
    public DateTime? LastPollTime { get; set; } = null;
}

/// <summary>
/// Represents a saved payload for random chaos mode.
/// </summary>
public class SavedPayload
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public string ExecutionCode { get; set; } = "";
    public string UndoCode { get; set; } = "";
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Settings for persisting stream state to allow auto-reconnect after restart.
/// </summary>
public class StreamStateSettings
{
    /// <summary>
    /// Whether the stream was live when the app last closed.
    /// </summary>
    public bool WasStreamLive { get; set; } = false;
    
    /// <summary>
    /// Whether YouTube was listening when the app last closed.
    /// </summary>
    public bool WasYouTubeListening { get; set; } = false;
    
    /// <summary>
    /// Whether Twitch was listening when the app last closed.
    /// </summary>
    public bool WasTwitchListening { get; set; } = false;
    
    /// <summary>
    /// The last known YouTube video ID.
    /// </summary>
    public string? LastYouTubeVideoId { get; set; }
    
    /// <summary>
    /// The last known Twitch channel.
    /// </summary>
    public string? LastTwitchChannel { get; set; }
    
    /// <summary>
    /// Timestamp when the stream state was last updated.
    /// </summary>
    public DateTime? LastUpdated { get; set; }
}
