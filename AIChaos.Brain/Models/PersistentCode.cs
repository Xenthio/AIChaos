using System.Text.Json.Serialization;

namespace AIChaos.Brain.Models;

/// <summary>
/// Represents a piece of code that persists across map changes.
/// Used for custom entities, weapons, and other semi-permanent game modifications.
/// </summary>
public class PersistentCodeEntry
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public PersistentCodeType Type { get; set; }
    public string Code { get; set; } = "";
    public string AuthorUserId { get; set; } = "";
    public string AuthorName { get; set; } = "anonymous";
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Original command ID that created this persistent code (if any).
    /// </summary>
    public int? OriginCommandId { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PersistentCodeType
{
    /// <summary>
    /// Scripted Entity (SENT) - custom entities like props, NPCs, etc.
    /// </summary>
    Entity,
    
    /// <summary>
    /// Scripted Weapon (SWEP) - custom weapons
    /// </summary>
    Weapon,
    
    /// <summary>
    /// Generic persistent code - hooks, global functions, etc.
    /// </summary>
    Generic,
    
    /// <summary>
    /// Custom gamemode modifications
    /// </summary>
    GameMode
}

/// <summary>
/// Request to create persistent code.
/// </summary>
public class CreatePersistentCodeRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public PersistentCodeType Type { get; set; }
    public string Code { get; set; } = "";
    public string? UserId { get; set; }
    public string? AuthorName { get; set; }
    public int? OriginCommandId { get; set; }
}

/// <summary>
/// Response containing persistent code entries.
/// </summary>
public class PersistentCodeResponse
{
    public List<PersistentCodeEntry> Entries { get; set; } = new();
}
