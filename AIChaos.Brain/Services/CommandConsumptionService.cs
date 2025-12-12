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
    
    public CommandConsumptionService(
        CommandQueueService commandQueue,
        ILogger<CommandConsumptionService> logger)
    {
        _commandQueue = commandQueue;
        _logger = logger;
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
        
        // Update command entry
        var command = _commandQueue.GetCommand(commandId);
        if (command != null)
        {
            command.ExecutionStartedAt = DateTime.UtcNow;
        }
        
        _logger.LogInformation("[CONSUMPTION] Command #{CommandId} started executing", commandId);
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
            
            var changeType = isSaveLoad ? "save load" : "level change";
            _logger.LogInformation("[CONSUMPTION] {ChangeType} detected: {Map}. {Count} commands will re-run.", 
                changeType, newMapName, response.PendingReruns.Count);
        }
        
        return response;
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
