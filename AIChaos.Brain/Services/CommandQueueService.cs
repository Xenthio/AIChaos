using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing the command queue and history.
/// </summary>
public class CommandQueueService
{
    private readonly List<string> _queue = new();
    private readonly List<CommandEntry> _history = new();
    private readonly List<SavedPayload> _savedPayloads = new();
    private readonly object _lock = new();
    private int _nextId = 1;
    private int _nextPayloadId = 1;
    
    private static readonly string SavedPayloadsDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "saved_payloads");
    private static readonly string SavedPayloadsFile = Path.Combine(SavedPayloadsDirectory, "payloads.json");
    
    public UserPreferences Preferences { get; } = new();
    
    public CommandQueueService()
    {
        LoadSavedPayloads();
    }
    
    /// <summary>
    /// Adds a command to the queue and history.
    /// </summary>
    public CommandEntry AddCommand(string userPrompt, string executionCode, string undoCode, string source = "web", string author = "anonymous", string? imageContext = null)
    {
        lock (_lock)
        {
            // Add to execution queue
            _queue.Add(executionCode);
            
            // Create history entry
            var entry = new CommandEntry
            {
                Id = _nextId++,
                Timestamp = DateTime.UtcNow,
                UserPrompt = userPrompt,
                ExecutionCode = executionCode,
                UndoCode = undoCode,
                ImageContext = imageContext,
                Source = source,
                Author = author,
                Status = CommandStatus.Queued
            };
            
            _history.Add(entry);
            
            // Trim history if needed
            while (_history.Count > Preferences.MaxHistoryLength)
            {
                _history.RemoveAt(0);
            }
            
            return entry;
        }
    }
    
    /// <summary>
    /// Polls for the next command in the queue.
    /// </summary>
    public string? PollNextCommand()
    {
        lock (_lock)
        {
            if (_queue.Count > 0)
            {
                var code = _queue[0];
                _queue.RemoveAt(0);
                return code;
            }
            return null;
        }
    }
    
    /// <summary>
    /// Gets the command history.
    /// </summary>
    public List<CommandEntry> GetHistory()
    {
        lock (_lock)
        {
            return new List<CommandEntry>(_history);
        }
    }
    
    /// <summary>
    /// Gets a command by ID.
    /// </summary>
    public CommandEntry? GetCommand(int id)
    {
        lock (_lock)
        {
            return _history.FirstOrDefault(c => c.Id == id);
        }
    }
    
    /// <summary>
    /// Queues the execution code for a previous command (repeat).
    /// </summary>
    public bool RepeatCommand(int commandId)
    {
        lock (_lock)
        {
            var command = _history.FirstOrDefault(c => c.Id == commandId);
            if (command == null) return false;
            
            _queue.Add(command.ExecutionCode);
            return true;
        }
    }
    
    /// <summary>
    /// Queues the undo code for a previous command.
    /// </summary>
    public bool UndoCommand(int commandId)
    {
        lock (_lock)
        {
            var command = _history.FirstOrDefault(c => c.Id == commandId);
            if (command == null) return false;
            
            _queue.Add(command.UndoCode);
            command.Status = CommandStatus.Undone;
            return true;
        }
    }
    
    /// <summary>
    /// Queues force undo code.
    /// </summary>
    public void QueueCode(string code)
    {
        lock (_lock)
        {
            _queue.Add(code);
        }
    }
    
    /// <summary>
    /// Clears the command history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
        }
    }
    
    /// <summary>
    /// Gets recent commands for AI context.
    /// </summary>
    public List<CommandEntry> GetRecentCommands(int count = 5)
    {
        lock (_lock)
        {
            return _history.TakeLast(count).ToList();
        }
    }
    
    /// <summary>
    /// Saves a command payload for random chaos mode.
    /// </summary>
    public SavedPayload SavePayload(CommandEntry command, string name)
    {
        lock (_lock)
        {
            var payload = new SavedPayload
            {
                Id = _nextPayloadId++,
                Name = string.IsNullOrEmpty(name) ? command.UserPrompt : name,
                UserPrompt = command.UserPrompt,
                ExecutionCode = command.ExecutionCode,
                UndoCode = command.UndoCode,
                SavedAt = DateTime.UtcNow
            };
            
            _savedPayloads.Add(payload);
            PersistSavedPayloads();
            return payload;
        }
    }
    
    /// <summary>
    /// Gets all saved payloads.
    /// </summary>
    public List<SavedPayload> GetSavedPayloads()
    {
        lock (_lock)
        {
            return new List<SavedPayload>(_savedPayloads);
        }
    }
    
    /// <summary>
    /// Deletes a saved payload.
    /// </summary>
    public bool DeletePayload(int payloadId)
    {
        lock (_lock)
        {
            var payload = _savedPayloads.FirstOrDefault(p => p.Id == payloadId);
            if (payload == null) return false;
            
            _savedPayloads.Remove(payload);
            PersistSavedPayloads();
            return true;
        }
    }
    
    /// <summary>
    /// Loads saved payloads from file on startup.
    /// </summary>
    private void LoadSavedPayloads()
    {
        try
        {
            if (File.Exists(SavedPayloadsFile))
            {
                var json = File.ReadAllText(SavedPayloadsFile);
                var payloads = JsonSerializer.Deserialize<List<SavedPayload>>(json);
                if (payloads != null)
                {
                    _savedPayloads.AddRange(payloads);
                    // Set next ID to be higher than any existing
                    if (_savedPayloads.Any())
                    {
                        _nextPayloadId = _savedPayloads.Max(p => p.Id) + 1;
                    }
                }
            }
        }
        catch
        {
            // If loading fails, start fresh
        }
    }
    
    /// <summary>
    /// Persists saved payloads to file.
    /// </summary>
    private void PersistSavedPayloads()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(SavedPayloadsDirectory);
            
            var json = JsonSerializer.Serialize(_savedPayloads, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(SavedPayloadsFile, json);
        }
        catch
        {
            // Silently ignore persistence errors
        }
    }
}
