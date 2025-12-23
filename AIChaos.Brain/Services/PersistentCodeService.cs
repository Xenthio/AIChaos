using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing persistent code that survives map changes.
/// Stores entity definitions, weapon definitions, and other semi-permanent code.
/// </summary>
public class PersistentCodeService
{
    private readonly List<PersistentCodeEntry> _entries = new();
    private readonly object _lock = new();
    private int _nextId = 1;
    private readonly ILogger<PersistentCodeService> _logger;
    
    private static readonly string PersistenceDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "persistent_code");
    private static readonly string PersistenceFile = Path.Combine(PersistenceDirectory, "persistent_code.json");
    
    // Event for when persistent code changes
    public event EventHandler? PersistentCodeChanged;
    
    public PersistentCodeService(ILogger<PersistentCodeService> logger)
    {
        _logger = logger;
        LoadFromFile();
    }
    
    /// <summary>
    /// Adds new persistent code.
    /// </summary>
    public PersistentCodeEntry AddPersistentCode(
        string name,
        string description,
        PersistentCodeType type,
        string code,
        string authorUserId,
        string authorName,
        int? originCommandId = null)
    {
        lock (_lock)
        {
            var entry = new PersistentCodeEntry
            {
                Id = _nextId++,
                CreatedAt = DateTime.UtcNow,
                Name = name,
                Description = description,
                Type = type,
                Code = code,
                AuthorUserId = authorUserId,
                AuthorName = authorName,
                IsActive = true,
                OriginCommandId = originCommandId
            };
            
            _entries.Add(entry);
            _logger.LogInformation("[PERSISTENT CODE] Added: {Name} (Type: {Type}, ID: {Id})", name, type, entry.Id);
            
            OnPersistentCodeChanged();
            return entry;
        }
    }
    
    /// <summary>
    /// Gets all persistent code entries.
    /// </summary>
    public List<PersistentCodeEntry> GetAll()
    {
        lock (_lock)
        {
            return new List<PersistentCodeEntry>(_entries);
        }
    }
    
    /// <summary>
    /// Gets all active persistent code entries.
    /// </summary>
    public List<PersistentCodeEntry> GetActive()
    {
        lock (_lock)
        {
            return _entries.Where(e => e.IsActive).ToList();
        }
    }
    
    /// <summary>
    /// Gets a specific persistent code entry by ID.
    /// </summary>
    public PersistentCodeEntry? GetById(int id)
    {
        lock (_lock)
        {
            return _entries.FirstOrDefault(e => e.Id == id);
        }
    }
    
    /// <summary>
    /// Deactivates a persistent code entry (soft delete).
    /// </summary>
    public bool Deactivate(int id)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry == null)
            {
                return false;
            }
            
            entry.IsActive = false;
            _logger.LogInformation("[PERSISTENT CODE] Deactivated: {Name} (ID: {Id})", entry.Name, id);
            
            OnPersistentCodeChanged();
            return true;
        }
    }
    
    /// <summary>
    /// Reactivates a persistent code entry.
    /// </summary>
    public bool Reactivate(int id)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry == null)
            {
                return false;
            }
            
            entry.IsActive = true;
            _logger.LogInformation("[PERSISTENT CODE] Reactivated: {Name} (ID: {Id})", entry.Name, id);
            
            OnPersistentCodeChanged();
            return true;
        }
    }
    
    /// <summary>
    /// Permanently deletes a persistent code entry.
    /// </summary>
    public bool Delete(int id)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry == null)
            {
                return false;
            }
            
            _entries.Remove(entry);
            _logger.LogInformation("[PERSISTENT CODE] Deleted: {Name} (ID: {Id})", entry.Name, id);
            
            OnPersistentCodeChanged();
            return true;
        }
    }
    
    /// <summary>
    /// Updates an existing persistent code entry.
    /// </summary>
    public bool Update(int id, string? name = null, string? description = null, string? code = null)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry == null)
            {
                return false;
            }
            
            if (name != null) entry.Name = name;
            if (description != null) entry.Description = description;
            if (code != null) entry.Code = code;
            
            _logger.LogInformation("[PERSISTENT CODE] Updated: {Name} (ID: {Id})", entry.Name, id);
            
            OnPersistentCodeChanged();
            return true;
        }
    }
    
    /// <summary>
    /// Gets all persistent code as a combined Lua script for execution.
    /// </summary>
    public string GetCombinedLuaScript()
    {
        lock (_lock)
        {
            var activeEntries = _entries.Where(e => e.IsActive).OrderBy(e => e.Id).ToList();
            
            if (activeEntries.Count == 0)
            {
                return "";
            }
            
            var script = new System.Text.StringBuilder();
            script.AppendLine("-- AI Chaos Persistent Code");
            script.AppendLine("-- Auto-generated on " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            script.AppendLine("-- DO NOT EDIT - This code is regenerated on every map load");
            script.AppendLine();
            
            foreach (var entry in activeEntries)
            {
                script.AppendLine($"-- ===== {entry.Name} =====");
                script.AppendLine($"-- Type: {entry.Type}");
                script.AppendLine($"-- Description: {entry.Description}");
                script.AppendLine($"-- Author: {entry.AuthorName}");
                script.AppendLine($"-- Created: {entry.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                script.AppendLine($"-- ID: {entry.Id}");
                script.AppendLine();
                script.AppendLine(entry.Code);
                script.AppendLine();
                script.AppendLine();
            }
            
            return script.ToString();
        }
    }
    
    private void OnPersistentCodeChanged()
    {
        PersistentCodeChanged?.Invoke(this, EventArgs.Empty);
        SaveToFile();
    }
    
    private void LoadFromFile()
    {
        try
        {
            if (!Directory.Exists(PersistenceDirectory))
            {
                Directory.CreateDirectory(PersistenceDirectory);
                _logger.LogInformation("[PERSISTENT CODE] Created persistence directory: {Dir}", PersistenceDirectory);
            }
            
            if (File.Exists(PersistenceFile))
            {
                var json = File.ReadAllText(PersistenceFile);
                var loaded = JsonSerializer.Deserialize<List<PersistentCodeEntry>>(json);
                
                if (loaded != null && loaded.Count > 0)
                {
                    _entries.Clear();
                    _entries.AddRange(loaded);
                    _nextId = _entries.Max(e => e.Id) + 1;
                    _logger.LogInformation("[PERSISTENT CODE] Loaded {Count} entries from persistence file", loaded.Count);
                }
            }
            else
            {
                _logger.LogInformation("[PERSISTENT CODE] No persistence file found, starting fresh");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PERSISTENT CODE] Failed to load from file");
        }
    }
    
    private void SaveToFile()
    {
        try
        {
            if (!Directory.Exists(PersistenceDirectory))
            {
                Directory.CreateDirectory(PersistenceDirectory);
            }
            
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(PersistenceFile, json);
            _logger.LogDebug("[PERSISTENT CODE] Saved {Count} entries to persistence file", _entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PERSISTENT CODE] Failed to save to file");
        }
    }
}
