using System.Collections.Concurrent;
using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for tracking command consumption and handling level change interruptions.
/// A command is considered "consumed" only after it has played uninterrupted for the required duration.
/// </summary>
public class CommandConsumptionService
{
    private readonly CommandQueueService _commandQueue;
    private readonly ILogger<CommandConsumptionService> _logger;
    
    // Track commands currently being executed (not yet consumed)
    private readonly ConcurrentDictionary<int, ExecutingCommand> _executingCommands = new();
    
    // Commands pending re-run after level change
    private readonly ConcurrentQueue<PendingRerunCommand> _pendingReruns = new();
    
    private readonly object _lock = new();
    private string _currentMap = "unknown";
    
    // Persistence paths
    private const string ExecutingStateDir = "queue_state";
    private const string ExecutingStateFile = "queue_state/executing.json";
    private readonly bool _enablePersistence;
    
    public CommandConsumptionService(
        CommandQueueService commandQueue,
        ILogger<CommandConsumptionService> logger,
        bool enablePersistence = true)
    {
        _commandQueue = commandQueue;
        _logger = logger;
        _enablePersistence = enablePersistence;
        
        // Load persisted executing commands on startup
        if (_enablePersistence)
        {
            LoadExecutingState();
        }
    }
    
    /// <summary>
    /// Loads executing commands from persistence file.
    /// </summary>
    private void LoadExecutingState()
    {
        try
        {
            if (File.Exists(ExecutingStateFile))
            {
                var json = File.ReadAllText(ExecutingStateFile);
                var commands = JsonSerializer.Deserialize<List<ExecutingCommandDto>>(json);
                if (commands != null)
                {
                    foreach (var cmd in commands)
                    {
                        var executingCommand = new ExecutingCommand
                        {
                            CommandId = cmd.CommandId,
                            Code = cmd.Code,
                            StartedAt = cmd.StartedAt
                        };
                        _executingCommands.TryAdd(cmd.CommandId, executingCommand);
                    }
                    _logger.LogInformation("[CONSUMPTION] Loaded {Count} executing commands from persistence", commands.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CONSUMPTION] Failed to load executing state");
        }
    }
    
    /// <summary>
    /// Saves executing commands to persistence file.
    /// </summary>
    private void SaveExecutingState()
    {
        if (!_enablePersistence) return;
        
        try
        {
            if (!Directory.Exists(ExecutingStateDir))
            {
                Directory.CreateDirectory(ExecutingStateDir);
            }
            
            var commands = _executingCommands.Values.Select(c => new ExecutingCommandDto
            {
                CommandId = c.CommandId,
                Code = c.Code,
                StartedAt = c.StartedAt
            }).ToList();
            
            var json = JsonSerializer.Serialize(commands, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ExecutingStateFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CONSUMPTION] Failed to save executing state");
        }
    }
    
    /// <summary>
    /// DTO for persisting executing commands.
    /// </summary>
    private class ExecutingCommandDto
    {
        public int CommandId { get; set; }
        public string Code { get; set; } = "";
        public DateTime StartedAt { get; set; }
    }
    
    /// <summary>
    /// Called when a command starts executing in the game.
    /// </summary>
    public void StartExecution(int commandId, string code)
    {
        var executingCommand = new ExecutingCommand
        {
            CommandId = commandId,
            Code = code,
            StartedAt = DateTime.UtcNow
        };
        
        // Use AddOrUpdate for thread-safe insertion
        _executingCommands.AddOrUpdate(commandId, executingCommand, (_, _) => executingCommand);
        
        // Persist executing state
        SaveExecutingState();
        
        // Update command entry
        var command = _commandQueue.GetCommand(commandId);
        if (command != null)
        {
            command.ExecutionStartedAt = DateTime.UtcNow;
        }
        
        _logger.LogInformation("[CONSUMPTION] Command #{CommandId} started executing at {Time}", 
            commandId, executingCommand.StartedAt);
    }
    
    /// <summary>
    /// Called when a command finishes executing successfully.
    /// Checks if the command has been consumed (played long enough).
    /// </summary>
    public bool CheckConsumption(int commandId)
    {
        if (!_executingCommands.TryGetValue(commandId, out var executing))
        {
            return false;
        }
        
        var elapsed = DateTime.UtcNow - executing.StartedAt;
        var isConsumed = elapsed.TotalSeconds >= Constants.Queue.ConsumptionTimeSeconds;
        
        if (isConsumed)
        {
            // Mark as consumed
            var command = _commandQueue.GetCommand(commandId);
            if (command != null)
            {
                command.IsConsumed = true;
            }
            
            _executingCommands.TryRemove(commandId, out _);
            SaveExecutingState();
            _logger.LogInformation("[CONSUMPTION] Command #{CommandId} consumed after {Seconds:F1}s", 
                commandId, elapsed.TotalSeconds);
        }
        
        return isConsumed;
    }
    
    /// <summary>
    /// Called when a level change or save load occurs.
    /// Marks all currently executing commands as interrupted.
    /// </summary>
    public LevelChangeResponse HandleLevelChange(string newMapName, bool isSaveLoad)
    {
        var response = new LevelChangeResponse { Status = "success" };
        
        lock (_lock)
        {
            _currentMap = newMapName;
            
            // Get snapshot of command IDs to process (thread-safe)
            var commandIds = _executingCommands.Keys.ToList();
            
            foreach (var commandId in commandIds)
            {
                // Try to get and remove the command atomically
                if (!_executingCommands.TryRemove(commandId, out var executing))
                {
                    continue; // Command was already removed by another thread
                }
                
                var elapsed = DateTime.UtcNow - executing.StartedAt;
                
                // Only interrupt if not yet consumed
                if (elapsed.TotalSeconds < Constants.Queue.ConsumptionTimeSeconds)
                {
                    // Update command entry
                    var command = _commandQueue.GetCommand(executing.CommandId);
                    if (command != null)
                    {
                        command.InterruptCount++;
                        _logger.LogInformation(
                            "[CONSUMPTION] Command #{CommandId} interrupted after {Seconds:F1}s (interrupt #{Count})", 
                            executing.CommandId, elapsed.TotalSeconds, command.InterruptCount);
                    }
                    
                    // Queue for re-run after level loads
                    var pendingRerun = new PendingRerunCommand
                    {
                        CommandId = executing.CommandId,
                        Code = executing.Code,
                        DelaySeconds = Constants.Queue.RerunDelayAfterLoadSeconds
                    };
                    
                    _pendingReruns.Enqueue(pendingRerun);
                    response.PendingReruns.Add(pendingRerun);
                }
                // If already consumed (elapsed >= threshold), just remove without re-queuing
            }
            
            // Save state after processing
            SaveExecutingState();
            
            var changeType = isSaveLoad ? "save load" : "level change";
            _logger.LogInformation("[CONSUMPTION] {ChangeType} detected: {Map}. {Count} commands will re-run.", 
                changeType, newMapName, response.PendingReruns.Count);
        }
        
        return response;
    }
    
    /// <summary>
    /// Called when we receive a shutdown timestamp from GMod.
    /// Marks commands that were executing at that time as interrupted.
    /// </summary>
    public void HandleLevelChangeFromTimestamp(DateTime shutdownTime)
    {
        lock (_lock)
        {
            // Get snapshot of command IDs to process (thread-safe)
            var commandIds = _executingCommands.Keys.ToList();
            
            _logger.LogInformation("[CONSUMPTION] Processing shutdown timestamp {Time}. Currently tracking {Count} executing commands: [{Ids}]", 
                shutdownTime, commandIds.Count, string.Join(", ", commandIds));
            
            foreach (var commandId in commandIds)
            {
                // Try to get and remove the command atomically
                if (!_executingCommands.TryRemove(commandId, out var executing))
                {
                    continue; // Command was already removed by another thread
                }
                
                // Calculate how long the command ran before shutdown
                var elapsed = shutdownTime - executing.StartedAt;
                
                _logger.LogInformation("[CONSUMPTION] Command #{CommandId}: StartedAt={StartedAt}, ShutdownTime={ShutdownTime}, Elapsed={Elapsed:F1}s, Threshold={Threshold}s", 
                    commandId, executing.StartedAt, shutdownTime, elapsed.TotalSeconds, Constants.Queue.ConsumptionTimeSeconds);
                
                // Only interrupt if not yet consumed at shutdown time
                if (elapsed.TotalSeconds < Constants.Queue.ConsumptionTimeSeconds)
                {
                    // Update command entry
                    var command = _commandQueue.GetCommand(executing.CommandId);
                    if (command != null)
                    {
                        command.InterruptCount++;
                        _logger.LogInformation(
                            "[CONSUMPTION] Command #{CommandId} interrupted at shutdown after {Seconds:F1}s (interrupt #{Count})", 
                            executing.CommandId, elapsed.TotalSeconds, command.InterruptCount);
                    }
                    
                    // Queue for re-run after level loads
                    var pendingRerun = new PendingRerunCommand
                    {
                        CommandId = executing.CommandId,
                        Code = executing.Code,
                        DelaySeconds = Constants.Queue.RerunDelayAfterLoadSeconds
                    };
                    
                    _pendingReruns.Enqueue(pendingRerun);
                }
                else
                {
                    _logger.LogInformation(
                        "[CONSUMPTION] Command #{CommandId} was consumed before shutdown ({Seconds:F1}s)", 
                        executing.CommandId, elapsed.TotalSeconds);
                }
            }
            
            // Save state after processing
            SaveExecutingState();
            
            _logger.LogInformation("[CONSUMPTION] Finished processing shutdown timestamp. {Count} commands pending rerun.", 
                _pendingReruns.Count);
        }
    }
    
    /// <summary>
    /// Gets and clears all commands pending re-run.
    /// Called by GMod after level finishes loading.
    /// </summary>
    public List<PendingRerunCommand> GetPendingReruns()
    {
        var reruns = new List<PendingRerunCommand>();
        
        while (_pendingReruns.TryDequeue(out var rerun))
        {
            reruns.Add(rerun);
        }
        
        return reruns;
    }
    
    /// <summary>
    /// Gets the current map name.
    /// </summary>
    public string GetCurrentMap() => _currentMap;
    
    /// <summary>
    /// Updates the current map name.
    /// </summary>
    public void SetCurrentMap(string mapName)
    {
        _currentMap = mapName;
    }
    
    /// <summary>
    /// Checks if a command is currently executing (not yet consumed).
    /// </summary>
    public bool IsExecuting(int commandId)
    {
        return _executingCommands.ContainsKey(commandId);
    }
    
    /// <summary>
    /// Gets the number of currently executing commands.
    /// </summary>
    public int GetExecutingCount()
    {
        return _executingCommands.Count;
    }
    
    /// <summary>
    /// Represents a command currently being executed.
    /// </summary>
    private class ExecutingCommand
    {
        public int CommandId { get; set; }
        public string Code { get; set; } = "";
        public DateTime StartedAt { get; set; }
    }
}
