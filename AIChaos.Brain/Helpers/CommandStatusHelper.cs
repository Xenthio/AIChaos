using AIChaos.Brain.Models;

namespace AIChaos.Brain.Helpers;

/// <summary>
/// Helper class for command status display logic.
/// </summary>
public static class CommandStatusHelper
{
    /// <summary>
    /// Gets the CSS class for a command status.
    /// </summary>
    public static string GetStatusClass(CommandStatus status) => status switch
    {
        CommandStatus.Executed => "executed",
        CommandStatus.Failed => "failed",
        CommandStatus.Queued => "queued",
        CommandStatus.Undone => "undone",
        CommandStatus.PendingModeration => "pendingmoderation",
        _ => "pending"
    };

    /// <summary>
    /// Gets the display text for a command status.
    /// </summary>
    public static string GetStatusText(CommandStatus status) => status.ToString();
}
