using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing the command queue and history.
/// </summary>
public class CommandQueueService
{
    private readonly List<(int CommandId, string Code)> _queue = new();
    private readonly List<CommandEntry> _history = new();
    private readonly List<SavedPayload> _savedPayloads = new();
    private readonly object _lock = new();
    private int _nextId = 1;
    private int _nextPayloadId = 1;
    
    private static readonly string SavedPayloadsDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "saved_payloads");
    private static readonly string SavedPayloadsFile = Path.Combine(SavedPayloadsDirectory, "payloads.json");
    
    public UserPreferences Preferences { get; } = new();
    
    // Event for when history changes
    public event EventHandler? HistoryChanged;
    
    public CommandQueueService()
    {
        LoadSavedPayloads();
    }
    
    private void OnHistoryChanged()
    {
        HistoryChanged?.Invoke(this, EventArgs.Empty);
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
            DeletePayloadFile(payload);
            return true;
        }
    }
    
    /// <summary>
    /// Loads saved payloads from files on startup.
    /// Migrates from old payloads.json format if it exists.
    /// </summary>
    private void LoadSavedPayloads()
    {
        try
        {
            // Check for old format (single payloads.json file) and migrate
            if (File.Exists(SavedPayloadsFile))
            {
                MigrateFromSingleFile();
            }
            
            // Load all individual payload files
            if (Directory.Exists(SavedPayloadsDirectory))
            {
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
        }
        catch
        {
            // If loading fails, start fresh
        }
    }
    
    /// <summary>
    /// Migrates from the old single payloads.json file to individual files.
    /// </summary>
    private void MigrateFromSingleFile()
    {
        try
        {
            var json = File.ReadAllText(SavedPayloadsFile);
            var payloads = JsonSerializer.Deserialize<List<SavedPayload>>(json);
            if (payloads != null && payloads.Any())
            {
                // Save each payload as an individual file
                foreach (var payload in payloads)
                {
                    PersistSinglePayload(payload);
                }
            }
            
            // Delete the old file after successful migration
            File.Delete(SavedPayloadsFile);
        }
        catch
        {
            // If migration fails, we'll just continue without it
        }
    }
    
    /// <summary>
    /// Persists a single saved payload to its own file.
    /// </summary>
    private void PersistSinglePayload(SavedPayload payload)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(SavedPayloadsDirectory);
            
            // Create a filename from the payload name (sanitized)
            var fileName = SanitizeFileName(payload.Name);
            var filePath = Path.Combine(SavedPayloadsDirectory, $"{fileName}_{payload.Id}.json");
            
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
    /// Deletes the file for a saved payload.
    /// </summary>
    private void DeletePayloadFile(SavedPayload payload)
    {
        try
        {
            if (Directory.Exists(SavedPayloadsDirectory))
            {
                // Find the file that matches this payload ID
                var files = Directory.GetFiles(SavedPayloadsDirectory, $"*_{payload.Id}.json");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Silently ignore deletion errors
        }
    }
    
    /// <summary>
    /// Sanitizes a filename by removing invalid characters.
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        // Get standard invalid characters plus some additional ones for safety
        var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
        // Add additional characters that might cause issues
        invalidChars.Add(':');
        invalidChars.Add('/');
        invalidChars.Add('\\');
        invalidChars.Add('<');
        invalidChars.Add('>');
        invalidChars.Add('"');
        invalidChars.Add('|');
        invalidChars.Add('?');
        invalidChars.Add('*');
        
        var sanitized = new string(fileName
            .Select(c => invalidChars.Contains(c) ? '_' : c)
            .ToArray());
        
        // Limit length and trim whitespace
        sanitized = sanitized.Trim();
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }
        
        // Replace multiple underscores with single underscore
        while (sanitized.Contains("__"))
        {
            sanitized = sanitized.Replace("__", "_");
        }
        
        return string.IsNullOrWhiteSpace(sanitized) ? "payload" : sanitized;
    }
}
