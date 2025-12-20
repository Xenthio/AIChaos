namespace AIChaos.Brain.Models;

/// <summary>
/// Shared constants for the Chaos application.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The cost in credits/USD for submitting an Idea.
    /// </summary>
    public const decimal CommandCost = 1.00m;

    /// <summary>
    /// Source identifiers for commands.
    /// </summary>
    public static class Sources
    {
        public const string Web = "web";
        public const string Twitch = "twitch";
        public const string YouTube = "youtube";
        public const string Discord = "discord";
        public const string Dashboard = "dashboard";
        public const string Favourite = "favourite";
        public const string HistoryRepeat = "history_repeat";
    }

    /// <summary>
    /// Default authors for commands.
    /// </summary>
    public static class Authors
    {
        public const string Anonymous = "anonymous";
        public const string Admin = "Admin";
    }

    /// <summary>
    /// Alert/message types for UI feedback.
    /// </summary>
    public static class AlertTypes
    {
        public const string Success = "success";
        public const string Error = "error";
        public const string Warning = "warning";
        public const string Info = "info";
    }

    /// <summary>
    /// Status message durations in milliseconds.
    /// </summary>
    public static class MessageDurations
    {
        public const int Short = 3000;
        public const int Medium = 5000;
        public const int Long = 10000;
    }

    /// <summary>
    /// User IDs and identifiers.
    /// </summary>
    public static class UserIds
    {
        public const string Admin = "admin";
    }

    /// <summary>
    /// Queue and consumption settings.
    /// </summary>
    public static class Queue
    {
        /// <summary>
        /// Time in seconds a command must play uninterrupted to be considered "consumed".
        /// </summary>
        public const int ConsumptionTimeSeconds = 20;

        /// <summary>
        /// Time in seconds to wait after a level loads before re-running an interrupted command.
        /// </summary>
        public const int RerunDelayAfterLoadSeconds = 5;
    }

    /// <summary>
    /// Safety limits for generated code.
    /// </summary>
    public static class Safety
    {
        /// <summary>
        /// Maximum duration in seconds for movement-blocking bindings.
        /// </summary>
        public const int MaxMovementBlockDurationSeconds = 10;
    }

    /// <summary>
    /// Redo system settings.
    /// </summary>
    public static class Redo
    {
        /// <summary>
        /// The cost in credits for a redo after the first free one.
        /// </summary>
        public const decimal RedoCost = 0.50m;
    }

    /// <summary>
    /// API throttling settings.
    /// </summary>
    public static class ApiThrottling
    {
        /// <summary>
        /// Maximum number of concurrent LLM API requests.
        /// Only this many requests will be sent to the API at a time.
        /// </summary>
        public const int MaxConcurrentRequests = 15;
    }
}
