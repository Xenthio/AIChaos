using System.Text.Json.Serialization;

namespace AIChaos.Brain.Models;

/// <summary>
/// Represents a command in the queue waiting to be executed by the game.
/// </summary>
public class CommandEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserPrompt { get; set; } = "";
    public string ExecutionCode { get; set; } = "";
    public string UndoCode { get; set; } = "";
    public string? ImageContext { get; set; }
    public string Source { get; set; } = "web"; // web, twitch, youtube
    public string Author { get; set; } = "anonymous";
    public string? UserId { get; set; } // Unique ID for web users to track their own commands
    public CommandStatus Status { get; set; } = CommandStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string? AiResponse { get; set; } // Non-code AI response message for the user
    public DateTime? ExecutedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommandStatus
{
    Pending,
    Queued,
    Executed,
    Undone,
    Failed
}

/// <summary>
/// Request to trigger a chaos command.
/// </summary>
public class TriggerRequest
{
    public string Prompt { get; set; } = "";
    public string? Source { get; set; }
    public string? Author { get; set; }
    public string? UserId { get; set; }
}

/// <summary>
/// Response from triggering a chaos command.
/// </summary>
public class TriggerResponse
{
    public string Status { get; set; } = "";
    public string? Message { get; set; }
    public string? CodePreview { get; set; }
    public bool HasUndo { get; set; }
    public int? CommandId { get; set; }
    public string? ContextFound { get; set; }
    public bool WasBlocked { get; set; }
    public string? AiResponse { get; set; } // Non-code response from AI to display to user
}

/// <summary>
/// Response from polling for commands.
/// </summary>
public class PollResponse
{
    [JsonPropertyName("has_code")]
    public bool HasCode { get; set; }
    
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    
    [JsonPropertyName("command_id")]
    public int? CommandId { get; set; }
}

/// <summary>
/// Request to repeat or undo a command.
/// </summary>
public class CommandIdRequest
{
    public int CommandId { get; set; }
}

/// <summary>
/// Generic API response.
/// </summary>
public class ApiResponse
{
    public string Status { get; set; } = "";
    public string? Message { get; set; }
    public int? CommandId { get; set; }
}

/// <summary>
/// History API response with command list and preferences.
/// </summary>
public class HistoryResponse
{
    public List<CommandEntry> History { get; set; } = new();
    public UserPreferences Preferences { get; set; } = new();
}

/// <summary>
/// User preferences for the application.
/// </summary>
public class UserPreferences
{
    public bool IncludeHistoryInAi { get; set; } = true;
    public bool HistoryEnabled { get; set; } = true;
    public int MaxHistoryLength { get; set; } = 50;
}

/// <summary>
/// OAuth state for Twitch authentication.
/// </summary>
public class TwitchAuthState
{
    public bool IsAuthenticated { get; set; }
    public string? Username { get; set; }
    public string? Channel { get; set; }
    public bool IsListening { get; set; }
}

/// <summary>
/// OAuth state for YouTube authentication.
/// </summary>
public class YouTubeAuthState
{
    public bool IsAuthenticated { get; set; }
    public string? ChannelName { get; set; }
    public string? VideoId { get; set; }
    public bool IsListening { get; set; }
}

/// <summary>
/// Tunnel state for ngrok/localtunnel/bore.
/// </summary>
public class TunnelState
{
    public bool IsRunning { get; set; }
    public string Type { get; set; } = "None";
    public string? Url { get; set; }
    public string? PublicIp { get; set; }
}

/// <summary>
/// Setup status response.
/// </summary>
public class SetupStatus
{
    public bool OpenRouterConfigured { get; set; }
    public bool AdminConfigured { get; set; }
    public string? CurrentModel { get; set; }
    public string AiProvider { get; set; } = "OpenRouter";
    public bool AiProviderConfigured { get; set; }
    public TwitchAuthState Twitch { get; set; } = new();
    public YouTubeAuthState YouTube { get; set; } = new();
    public TunnelState Tunnel { get; set; } = new();
}

/// <summary>
/// Request to save a command payload.
/// </summary>
public class SavePayloadRequest
{
    public int CommandId { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// Request to delete a saved payload.
/// </summary>
public class DeletePayloadRequest
{
    public int PayloadId { get; set; }
}

/// <summary>
/// Response containing saved payloads.
/// </summary>
public class SavedPayloadsResponse
{
    public List<SavedPayload> Payloads { get; set; } = new();
}

/// <summary>
/// Request to report command execution result from GMod.
/// </summary>
public class ExecutionResultRequest
{
    [JsonPropertyName("command_id")]
    public int CommandId { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
