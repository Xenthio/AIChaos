namespace AIChaos.Brain.Models;

/// <summary>
/// Configuration settings for the AI Chaos Brain application.
/// </summary>
public class AppSettings
{
    public AiSettings Ai { get; set; } = new();
    public OpenRouterSettings OpenRouter { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
    public OobaSettings Oobabooga { get; set; } = new();
    public TwitchSettings Twitch { get; set; } = new();
    public YouTubeSettings YouTube { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public AdminSettings Admin { get; set; } = new();
    public TunnelSettings Tunnel { get; set; } = new();
    public TestClientSettings TestClient { get; set; } = new();
}

/// <summary>
/// AI provider configuration.
/// </summary>
public class AiSettings
{
    /// <summary>
    /// The AI provider to use (OpenRouter, Ollama, or Oobabooga).
    /// </summary>
    public AiProvider Provider { get; set; } = AiProvider.OpenRouter;
}

/// <summary>
/// Available AI providers.
/// </summary>
public enum AiProvider
{
    /// <summary>
    /// OpenRouter API (default) - cloud-based API with multiple model access.
    /// </summary>
    OpenRouter,
    
    /// <summary>
    /// Ollama - local AI model server.
    /// </summary>
    Ollama,
    
    /// <summary>
    /// Oobabooga (text-generation-webui) - local AI with OpenAI-compatible API.
    /// </summary>
    Oobabooga
}

public class OpenRouterSettings
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string Model { get; set; } = "anthropic/claude-sonnet-4.5";
}

/// <summary>
/// Ollama local AI server settings.
/// </summary>
public class OllamaSettings
{
    /// <summary>
    /// Base URL for Ollama API (default: http://localhost:11434).
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";
    
    /// <summary>
    /// Model name to use (e.g., llama3, codellama, mistral).
    /// </summary>
    public string Model { get; set; } = "llama3";
}

/// <summary>
/// Oobabooga (text-generation-webui) settings with OpenAI-compatible API.
/// </summary>
public class OobaSettings
{
    /// <summary>
    /// Base URL for Oobabooga API (default: http://localhost:5001/v1).
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5001/v1";
    
    /// <summary>
    /// Optional API key if authentication is enabled.
    /// </summary>
    public string ApiKey { get; set; } = "";
    
    /// <summary>
    /// Model name (optional - depends on loaded model in text-generation-webui).
    /// </summary>
    public string Model { get; set; } = "";
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
}

public class SafetySettings
{
    public bool BlockUrls { get; set; } = true;
    public List<string> AllowedDomains { get; set; } = new() { "i.imgur.com", "imgur.com" };
    public List<string> Moderators { get; set; } = new();
    public bool PrivateDiscordMode { get; set; } = false;
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
