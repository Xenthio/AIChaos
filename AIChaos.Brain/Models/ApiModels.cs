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
    
    /// <summary>
    /// Whether this command has been fully consumed (played uninterrupted for required duration).
    /// </summary>
    public bool IsConsumed { get; set; } = false;
    
    /// <summary>
    /// When the command started executing (for consumption tracking).
    /// </summary>
    public DateTime? ExecutionStartedAt { get; set; }
    
    /// <summary>
    /// Number of times this command was interrupted by level changes.
    /// </summary>
    public int InterruptCount { get; set; } = 0;
    
    /// <summary>
    /// Whether this is a redo of a previous command.
    /// </summary>
    public bool IsRedo { get; set; } = false;
    
    /// <summary>
    /// The original command ID if this is a redo.
    /// </summary>
    public int? OriginalCommandId { get; set; }
    
    /// <summary>
    /// User-provided feedback explaining why the command failed (for redo).
    /// </summary>
    public string? RedoFeedback { get; set; }
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
/// Represents content (URLs, images, external links) pending moderation review.
/// </summary>
public class PendingPromptEntry
{
    public int Id { get; set; }
    public int? CommandId { get; set; } // Link to the command entry in history
    public string ContentUrl { get; set; } = ""; // URL or content that needs review
    public string UserPrompt { get; set; } = "";
    public string Source { get; set; } = "web";
    public string Author { get; set; } = "anonymous";
    public string? UserId { get; set; }
    public string FilterReason { get; set; } = "URL detected"; // Why this needs moderation
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public PromptModerationStatus Status { get; set; } = PromptModerationStatus.Pending;
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>
/// Status of content in the moderation queue.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PromptModerationStatus
{
    Pending,
    Approved,
    Denied
}

/// <summary>
/// Response for pending prompts list.
/// </summary>
public class PendingPromptsResponse
{
    public List<PendingPromptEntry> Prompts { get; set; } = new();
    public int TotalPending { get; set; }
}

/// <summary>
/// Request to review content (approve/deny).
/// </summary>
public class PromptReviewRequest
{
    public int PromptId { get; set; }
    public bool Approved { get; set; }
}

/// <summary>
/// Represents code pending moderation review due to filtered content.
/// </summary>
public class PendingCodeEntry
{
    public int Id { get; set; }
    public int? CommandId { get; set; } // Link to the command entry in history
    public string UserPrompt { get; set; } = "";
    public string ExecutionCode { get; set; } = "";
    public string UndoCode { get; set; } = "";
    public string Source { get; set; } = "web";
    public string Author { get; set; } = "anonymous";
    public string? UserId { get; set; }
    public string FilterReason { get; set; } = ""; // What triggered the filter
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public CodeModerationStatus Status { get; set; } = CodeModerationStatus.Pending;
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>
/// Status of code in the moderation queue.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CodeModerationStatus
{
    Pending,
    Approved,
    Denied
}

/// <summary>
/// Request to review code (approve/deny).
/// </summary>
public class CodeReviewRequest
{
    public int CodeId { get; set; }
    public bool Approved { get; set; }
}

/// <summary>
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
/// Request to redo a failed command with user feedback.
/// </summary>
public class RedoRequest
{
    /// <summary>
    /// The ID of the command to redo.
    /// </summary>
    public int CommandId { get; set; }
    
    /// <summary>
    /// User-provided explanation of what went wrong.
    /// </summary>
    public string Feedback { get; set; } = "";
}

/// <summary>
/// Response from a redo request.
/// </summary>
public class RedoResponse
{
    public string Status { get; set; } = "";
    public string? Message { get; set; }
    public int? NewCommandId { get; set; }
    public decimal NewBalance { get; set; }
    public bool WasFree { get; set; }
}

/// <summary>
/// Notification from GMod about level/map changes.
/// </summary>
public class LevelChangeRequest
{
    [JsonPropertyName("map_name")]
    public string MapName { get; set; } = "";
    
    [JsonPropertyName("is_save_load")]
    public bool IsSaveLoad { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request for pending re-runs after level load.
/// </summary>
public class PendingRerunsRequest
{
    /// <summary>
    /// Unix timestamp of when shutdown occurred.
    /// If provided, server will mark commands executing at that time as interrupted.
    /// </summary>
    [JsonPropertyName("shutdown_timestamp")]
    public long? ShutdownTimestamp { get; set; }
}

/// <summary>
/// Response to level change notification.
/// </summary>
public class LevelChangeResponse
{
    public string Status { get; set; } = "";
    
    /// <summary>
    /// Commands that need to be re-run after the level loads.
    /// </summary>
    public List<PendingRerunCommand> PendingReruns { get; set; } = new();
}

/// <summary>
/// A command pending re-run after level change.
/// </summary>
public class PendingRerunCommand
{
    public int CommandId { get; set; }
    public string Code { get; set; } = "";
    
    /// <summary>
    /// Delay in seconds before running this command after level loads.
    /// </summary>
    public int DelaySeconds { get; set; }
}

/// <summary>
/// Response containing persistent code script for GMod.
/// </summary>
public class PersistentCodeScriptResponse
{
    [JsonPropertyName("has_code")]
    public bool HasCode { get; set; }
    
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    
    [JsonPropertyName("active_count")]
    public int ActiveCount { get; set; }
}

