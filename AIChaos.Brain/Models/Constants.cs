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
}
