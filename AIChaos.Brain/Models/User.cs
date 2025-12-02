using System.Text.Json.Serialization;

namespace AIChaos.Brain.Models;

/// <summary>
/// Represents a user in the chaos system.
/// </summary>
public class User
{
    /// <summary>
    /// Unique ID (e.g., YouTube Channel ID).
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Display name.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Current credit balance (in USD).
    /// </summary>
    public decimal CreditBalance { get; set; }

    /// <summary>
    /// Total amount spent (lifetime).
    /// </summary>
    public decimal TotalSpent { get; set; }

    /// <summary>
    /// Timestamp of the last command submission.
    /// </summary>
    public DateTime LastRequestTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Platform (youtube, twitch, etc).
    /// </summary>
    public string Platform { get; set; } = "youtube";
}
