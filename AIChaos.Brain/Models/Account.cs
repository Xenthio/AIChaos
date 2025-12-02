using System.Text.Json.Serialization;

namespace AIChaos.Brain.Models;

/// <summary>
/// Represents a user account in the chaos system.
/// </summary>
public class Account
{
    /// <summary>
    /// Unique account ID (GUID).
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Username for login.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Hashed password.
    /// </summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>
    /// Display name shown in UI.
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
    /// Timestamp of the last command submission (for rate limiting).
    /// </summary>
    public DateTime LastRequestTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Linked YouTube Channel ID (null if not linked).
    /// </summary>
    public string? LinkedYouTubeChannelId { get; set; }
    
    /// <summary>
    /// Profile picture URL (from Google Sign-In).
    /// </summary>
    public string? PictureUrl { get; set; }

    /// <summary>
    /// Pending verification code for linking YouTube (null if no pending link).
    /// </summary>
    public string? PendingVerificationCode { get; set; }

    /// <summary>
    /// When the verification code expires.
    /// </summary>
    public DateTime? VerificationCodeExpiresAt { get; set; }

    /// <summary>
    /// Account creation date.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current session token (for staying logged in).
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// When the session token expires.
    /// </summary>
    public DateTime? SessionExpiresAt { get; set; }
}
