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
    PendingModeration,  // Waiting for image moderation approval
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
    public bool InteractiveModeEnabled { get; set; } = false;
    public int InteractiveMaxIterations { get; set; } = 5;
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
    public TwitchAuthState Twitch { get; set; } = new();
    public YouTubeAuthState YouTube { get; set; } = new();
    public TunnelState Tunnel { get; set; } = new();
    public TestClientState TestClient { get; set; } = new();
}

/// <summary>
/// Test client state for multirun mode.
/// </summary>
public class TestClientState
{
    public bool Enabled { get; set; }
    public bool IsConnected { get; set; }
    public string TestMap { get; set; } = "gm_flatgrass";
    public bool CleanupAfterTest { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
    public string? GmodPath { get; set; }
    public DateTime? LastPollTime { get; set; }
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
    
    [JsonPropertyName("result_data")]
    public string? ResultData { get; set; }
}

/// <summary>
/// Request to trigger an interactive chat session.
/// </summary>
public class InteractiveTriggerRequest
{
    public string Prompt { get; set; } = "";
    public string? Source { get; set; }
    public string? Author { get; set; }
    public string? UserId { get; set; }
    public int MaxIterations { get; set; } = 5;
}

/// <summary>
/// Response from an interactive chat session.
/// </summary>
public class InteractiveSessionResponse
{
    public string Status { get; set; } = "";
    public string? Message { get; set; }
    public int SessionId { get; set; }
    public int Iteration { get; set; }
    public string? CurrentPhase { get; set; }
    public bool IsComplete { get; set; }
    public string? FinalCode { get; set; }
    public List<InteractionStep> Steps { get; set; } = new();
}

/// <summary>
/// A single step in an interactive session.
/// </summary>
public class InteractionStep
{
    public int StepNumber { get; set; }
    public string Phase { get; set; } = "";
    public string? Code { get; set; }
    public bool? Success { get; set; }
    public string? Error { get; set; }
    public string? ResultData { get; set; }
    public string? AiThinking { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An interactive AI session that can iterate with the game.
/// </summary>
public class InteractiveSession
{
    public int Id { get; set; }
    public string UserPrompt { get; set; } = "";
    public string Source { get; set; } = "web";
    public string Author { get; set; } = "anonymous";
    public string? UserId { get; set; }
    public int MaxIterations { get; set; } = 5;
    public int CurrentIteration { get; set; } = 0;
    public InteractivePhase CurrentPhase { get; set; } = InteractivePhase.Preparing;
    public bool IsComplete { get; set; } = false;
    public bool WasSuccessful { get; set; } = false;
    public string? FinalExecutionCode { get; set; }
    public string? FinalUndoCode { get; set; }
    public List<InteractionStep> Steps { get; set; } = new();
    public List<ChatMessage> ConversationHistory { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    // Pending execution state
    public int? PendingCommandId { get; set; }
    public string? PendingCode { get; set; }
}

/// <summary>
/// Phases of an interactive session.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InteractivePhase
{
    Preparing,      // AI is gathering information
    Generating,     // AI is generating main code
    Testing,        // Code is being tested
    Fixing,         // AI is fixing errors
    Complete,       // Session finished successfully
    Failed          // Session failed after max iterations
}

/// <summary>
/// A message in the AI conversation history.
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

/// <summary>
/// Represents an image pending moderation review.
/// </summary>
public class PendingImageEntry
{
    public int Id { get; set; }
    public int? CommandId { get; set; } // Link to the command entry in history
    public string ImageUrl { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public string Source { get; set; } = "web";
    public string Author { get; set; } = "anonymous";
    public string? UserId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public ImageModerationStatus Status { get; set; } = ImageModerationStatus.Pending;
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>
/// Status of an image in the moderation queue.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageModerationStatus
{
    Pending,
    Approved,
    Denied
}

/// <summary>
/// Response for pending images list.
/// </summary>
public class PendingImagesResponse
{
    public List<PendingImageEntry> Images { get; set; } = new();
    public int TotalPending { get; set; }
}

/// <summary>
/// Request to review an image (approve/deny).
/// </summary>
public class ImageReviewRequest
{
    public int ImageId { get; set; }
    public bool Approved { get; set; }
}
/// Request to report test result from test client.
/// </summary>
public class TestResultRequest
{
    [JsonPropertyName("command_id")]
    public int CommandId { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
    
    [JsonPropertyName("is_test_client")]
    public bool IsTestClient { get; set; }
}

/// <summary>
/// Response for agentic session.
/// </summary>
public class AgentSessionResponse
{
    public string Status { get; set; } = "";
    public string? Message { get; set; }
    public int SessionId { get; set; }
    public string? Mode { get; set; }
    public int Iteration { get; set; }
    public string? CurrentPhase { get; set; }
    public bool IsComplete { get; set; }
    public string? FinalCode { get; set; }
    public List<AgentStepResponse> Steps { get; set; } = new();
}

/// <summary>
/// Response for a single agent step.
/// </summary>
public class AgentStepResponse
{
    public int StepNumber { get; set; }
    public string Phase { get; set; } = "";
    public string? Code { get; set; }
    public bool? Success { get; set; }
    public string? Error { get; set; }
    public string? ResultData { get; set; }
    public string? AiThinking { get; set; }
}
