namespace AIChaos.Brain.Models;

/// <summary>
/// Tracks credits earned by a YouTube channel that hasn't been linked to an account yet.
/// Once the channel is linked, these credits are transferred to the account.
/// </summary>
public class PendingChannelCredits
{
    /// <summary>
    /// YouTube Channel ID.
    /// </summary>
    public string ChannelId { get; set; } = "";
    
    /// <summary>
    /// Display name of the channel.
    /// </summary>
    public string DisplayName { get; set; } = "";
    
    /// <summary>
    /// Total pending credits (in USD).
    /// </summary>
    public decimal PendingBalance { get; set; }
    
    /// <summary>
    /// Individual donation records.
    /// </summary>
    public List<DonationRecord> Donations { get; set; } = new();
}

/// <summary>
/// Record of a single donation/Super Chat.
/// </summary>
public class DonationRecord
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public string Source { get; set; } = "superchat"; // superchat, membership, etc.
    public string? Message { get; set; }
}
