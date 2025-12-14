using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing the command queue and history.
/// Queue state is persisted to JSON to survive application restarts.
/// </summary>
public class CommandQueueService
{
    private readonly List<(int CommandId, string Code)> _queue = new();
    private readonly List<CommandEntry> _history = new();
    private readonly List<SavedPayload> _savedPayloads = new();
    private readonly object _lock = new();
    private int _nextId = 1;
    private int _nextPayloadId = 1;
    private readonly bool _enablePersistence;
    
    private static readonly string SavedPayloadsDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "saved_payloads");
    
    // Queue persistence paths
    private static readonly string QueuePersistenceDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "queue_state");
    private static readonly string QueueStateFile = Path.Combine(QueuePersistenceDirectory, "queue.json");
    private static readonly string HistoryStateFile = Path.Combine(QueuePersistenceDirectory, "history.json");
    
    public UserPreferences Preferences { get; } = new();
    
    // Event for when history changes
    public event EventHandler? HistoryChanged;
    
    /// <summary>
    /// Creates a new CommandQueueService with persistence enabled by default.
    /// </summary>
    public CommandQueueService() : this(enablePersistence: true)
    {
    }
    
    /// <summary>
    /// Creates a new CommandQueueService with optional persistence.
    /// </summary>
    /// <param name="enablePersistence">When false, skips loading and saving state to disk (useful for testing).</param>
    public CommandQueueService(bool enablePersistence)
    {
        _enablePersistence = enablePersistence;
        if (_enablePersistence)
        {
            LoadSavedPayloads();
            LoadQueueState();
        }
    }
    
    private void OnHistoryChanged()
    {
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        if (_enablePersistence)
        {
            PersistQueueState();
        }
    }
    
    /// <summary>
    /// Adds a command to the queue and history.
    /// </summary>
    public CommandEntry AddCommand(string userPrompt, string executionCode, string undoCode, string source = "web", string author = "anonymous", string? imageContext = null, string? userId = null, string? aiResponse = null)
    {
        return AddCommand(userPrompt, executionCode, undoCode, source, author, imageContext, userId, aiResponse, queueForExecution: true);
    }
    
    /// <summary>
    /// Adds a command to history, optionally queueing it for execution.
    /// </summary>
    public CommandEntry AddCommand(string userPrompt, string executionCode, string undoCode, string source, string author, string? imageContext, string? userId, string? aiResponse, bool queueForExecution)
    {
        lock (_lock)
        {
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
                UserId = userId,
                AiResponse = aiResponse,
                Status = CommandStatus.Queued
            };
            
            // Add to execution queue with ID (only if requested)
            if (queueForExecution)
            {
                _queue.Add((entry.Id, executionCode));
            }
            
            _history.Add(entry);
            
            // Trim history if needed
            while (_history.Count > Preferences.MaxHistoryLength)
            {
                _history.RemoveAt(0);
            }
            
            OnHistoryChanged(); // Notify that history has changed
            
            return entry;
        }
    }
    
    /// <summary>
    /// Adds a command to history with a specific status.
    /// </summary>
    public CommandEntry AddCommandWithStatus(string userPrompt, string executionCode, string undoCode, string source, string author, string? imageContext, string? userId, string? aiResponse, CommandStatus status, bool queueForExecution = false)
    {
        lock (_lock)
        {
            // Create history entry with specified status
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
                UserId = userId,
                AiResponse = aiResponse,
                Status = status
            };
            
            // Add to execution queue with ID (only if requested)
            if (queueForExecution)
            {
                _queue.Add((entry.Id, executionCode));
            }
            
            _history.Add(entry);
            
            // Trim history if needed
            while (_history.Count > Preferences.MaxHistoryLength)
            {
                _history.RemoveAt(0);
            }
            
            OnHistoryChanged();
            return entry;
        }
    }
    
    /// <summary>
    /// Polls for the next command in the queue.
    /// Returns both the command ID and code so GMod can report back results.
    /// </summary>
    public (int CommandId, string Code)? PollNextCommand()
    {
        lock (_lock)
        {
            if (_queue.Count > 0)
            {
                var item = _queue[0];
                _queue.RemoveAt(0);
                return item;
            }
            return null;
        }
    }
    
    /// <summary>
    /// Peeks at the next command without removing it from the queue.
    /// </summary>
    public (int CommandId, string Code)? PeekNextCommand()
    {
        lock (_lock)
        {
            if (_queue.Count > 0)
            {
                return _queue[0];
            }
            return null;
        }
    }
    
    /// <summary>
    /// Gets the current queue count.
    /// </summary>
    public int GetQueueCount()
    {
        lock (_lock)
        {
            return _queue.Count;
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
    /// Gets command history filtered by user ID.
    /// </summary>
    public List<CommandEntry> GetHistoryForUser(string userId)
    {
        lock (_lock)
        {
            return _history.Where(c => c.UserId == userId).ToList();
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
    /// Updates a command's status and optionally other properties.
    /// Fires HistoryChanged event to notify subscribers.
    /// </summary>
    public bool UpdateCommand(int commandId, CommandStatus status, string? executionCode = null, string? undoCode = null, string? imageContext = null, string? aiResponse = null)
    {
        lock (_lock)
        {
            var command = _history.FirstOrDefault(c => c.Id == commandId);
            if (command == null) return false;
            
            command.Status = status;
            
            if (executionCode != null)
                command.ExecutionCode = executionCode;
            if (undoCode != null)
                command.UndoCode = undoCode;
            if (imageContext != null)
                command.ImageContext = imageContext;
            if (aiResponse != null)
                command.AiResponse = aiResponse;
            
            OnHistoryChanged();
            return true;
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
            
            _queue.Add((commandId, command.ExecutionCode));
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
            
            _queue.Add((commandId, command.UndoCode));
            command.Status = CommandStatus.Undone;
            return true;
        }
    }
    
    /// <summary>
    /// Queues force undo code (no command ID tracking).
    /// </summary>
    public void QueueCode(string code)
    {
        lock (_lock)
        {
            // Use -1 as command ID for ad-hoc code (force undo, etc.)
            _queue.Add((-1, code));
        }
    }
    
    /// <summary>
    /// Queues code for interactive sessions with a specific command ID.
    /// </summary>
    public void QueueInteractiveCode(int commandId, string code)
    {
        lock (_lock)
        {
            _queue.Add((commandId, code));
        }
    }
    
    /// <summary>
    /// Queues an existing command for execution.
    /// </summary>
    public void QueueCommand(CommandEntry command)
    {
        lock (_lock)
        {
            _queue.Add((command.Id, command.ExecutionCode));
            OnHistoryChanged();
        }
    }
    
    /// <summary>
    /// Reports the execution result from GMod.
    /// </summary>
    public bool ReportExecutionResult(int commandId, bool success, string? error)
    {
        lock (_lock)
        {
            var command = _history.FirstOrDefault(c => c.Id == commandId);
            if (command == null) return false;
            
            command.ExecutedAt = DateTime.UtcNow;
            if (success)
            {
                command.Status = CommandStatus.Executed;
            }
            else
            {
                command.Status = CommandStatus.Failed;
                command.ErrorMessage = error;
            }
            
            OnHistoryChanged();
            return true;
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
    /// Each payload is saved as its own JSON file for easy merging/transfer.
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
            PersistSinglePayload(payload);
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
            DeleteSinglePayload(payload.Id);
            return true;
        }
    }
    
    /// <summary>
    /// Loads saved payloads from individual JSON files on startup.
    /// Each payload is stored in its own file for easy merging/transfer between installations.
    /// </summary>
    private void LoadSavedPayloads()
    {
        try
        {
            if (!Directory.Exists(SavedPayloadsDirectory))
                return;
            
            // Load each .json file in the directory
            var files = Directory.GetFiles(SavedPayloadsDirectory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var payload = JsonSerializer.Deserialize<SavedPayload>(json);
                    if (payload != null)
                    {
                        _savedPayloads.Add(payload);
                    }
                }
                catch
                {
                    // Skip files that can't be parsed
                }
            }
            
            // Set next ID to be higher than any existing
            if (_savedPayloads.Any())
            {
                _nextPayloadId = _savedPayloads.Max(p => p.Id) + 1;
            }
        }
        catch
        {
            // If loading fails, start fresh
        }
    }
    
    /// <summary>
    /// Persists a single saved payload to its own JSON file.
    /// </summary>
    private void PersistSinglePayload(SavedPayload payload)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(SavedPayloadsDirectory);
            
            // Generate safe filename from payload name or ID
            var safeName = string.IsNullOrWhiteSpace(payload.Name)
                ? $"payload_{payload.Id}"
                : SanitizeFileName(payload.Name);
            var fileName = $"{payload.Id}_{safeName}.json";
            var filePath = Path.Combine(SavedPayloadsDirectory, fileName);
            
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Silently ignore persistence errors
        }
    }
    
    /// <summary>
    /// Deletes a saved payload's JSON file.
    /// </summary>
    private void DeleteSinglePayload(int payloadId)
    {
        try
        {
            if (!Directory.Exists(SavedPayloadsDirectory))
                return;
            
            // Find and delete the file with matching ID prefix
            var files = Directory.GetFiles(SavedPayloadsDirectory, $"{payloadId}_*.json");
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
        catch
        {
            // Silently ignore deletion errors
        }
    }
    
    /// <summary>
    /// Sanitizes a string for use as a filename.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Where(c => !invalid.Contains(c)));
        // Limit length and replace spaces
        sanitized = sanitized.Replace(' ', '_');
        if (sanitized.Length > 50)
            sanitized = sanitized.Substring(0, 50);
        return sanitized;
    }
    
    /// <summary>
    /// Loads queue and history state from disk on startup.
    /// Ensures queued requests are not lost if application restarts.
    /// </summary>
    private void LoadQueueState()
    {
        try
        {
            // Load history first
            if (File.Exists(HistoryStateFile))
            {
                var historyJson = File.ReadAllText(HistoryStateFile);
                var history = JsonSerializer.Deserialize<List<CommandEntry>>(historyJson);
                if (history != null)
                {
                    _history.AddRange(history);
                    if (_history.Any())
                    {
                        _nextId = _history.Max(h => h.Id) + 1;
                    }
                }
            }
            
            // Load queue
            if (File.Exists(QueueStateFile))
            {
                var queueJson = File.ReadAllText(QueueStateFile);
                var queueItems = JsonSerializer.Deserialize<List<QueueItem>>(queueJson);
                if (queueItems != null)
                {
                    foreach (var item in queueItems)
                    {
                        _queue.Add((item.CommandId, item.Code));
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
    /// Persists queue and history state to disk.
    /// Called whenever the queue or history changes.
    /// </summary>
    private void PersistQueueState()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(QueuePersistenceDirectory);
            
            // Save queue
            var queueItems = _queue.Select(q => new QueueItem { CommandId = q.CommandId, Code = q.Code }).ToList();
            var queueJson = JsonSerializer.Serialize(queueItems, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(QueueStateFile, queueJson);
            
            // Save history
            var historyJson = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(HistoryStateFile, historyJson);
        }
        catch
        {
            // Silently ignore persistence errors
        }
    }
    
    /// <summary>
    /// Helper class for queue serialization.
    /// </summary>
    private class QueueItem
    {
        public int CommandId { get; set; }
        public string Code { get; set; } = "";
    }
}
