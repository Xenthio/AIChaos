using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing the concurrent slot-based queue execution system.
/// Implements dynamic scaling based on queue depth with time-based slot blocking.
/// </summary>
public class QueueSlotService
{
    private readonly CommandQueueService _commandQueue;
    private readonly ILogger<QueueSlotService> _logger;
    
    // Slot management
    private readonly List<ExecutionSlot> _slots = new();
    private readonly object _lock = new();
    
    // Configuration
    private const int DefaultSlotBlockSeconds = 25; // Default 25 seconds between executions per slot
    private const int MinSlots = 3;
    private const int MaxSlots = 10;
    private const int LowVolumeThreshold = 5;
    private const int HighVolumeThreshold = 50;
    
    public QueueSlotService(
        CommandQueueService commandQueue,
        ILogger<QueueSlotService> logger)
    {
        _commandQueue = commandQueue;
        _logger = logger;
        
        // Initialize with minimum slots
        for (int i = 0; i < MinSlots; i++)
        {
            _slots.Add(new ExecutionSlot { Id = i + 1 });
        }
    }
    
    /// <summary>
    /// Polls for the next command respecting slot availability and queue depth.
    /// Returns null if no slots are available or queue is empty.
    /// </summary>
    public (int CommandId, string Code)? PollNextCommand()
    {
        lock (_lock)
        {
            // First, check if there are any queued commands
            if (_commandQueue.GetQueueCount() == 0)
            {
                // No commands in queue
                return null;
            }
            
            // Try to find an available slot
            var availableSlot = GetAvailableSlot();
            
            if (availableSlot == null)
            {
                // No slots available - return null to enforce rate limiting
                _logger.LogDebug("[QUEUE] No available slots. {Count} command(s) waiting.", 
                    _commandQueue.GetQueueCount());
                return null;
            }
            
            // Get the next command from the queue
            var result = _commandQueue.PollNextCommand();
            if (!result.HasValue)
            {
                // Race condition - queue was empty by the time we polled
                return null;
            }
            
            // Occupy the slot
            availableSlot.IsOccupied = true;
            availableSlot.LastExecutionTime = DateTime.UtcNow;
            availableSlot.CurrentCommandId = result.Value.CommandId;
            
            _logger.LogInformation("[QUEUE] Slot {SlotId} executing command #{CommandId} ({QueueRemaining} remaining)", 
                availableSlot.Id, result.Value.CommandId, _commandQueue.GetQueueCount());
            
            // Adjust slot count based on queue depth
            AdjustSlotCount();
            
            return result;
        }
    }
    
    /// <summary>
    /// Manually blasts the next item(s) in queue, bypassing all slot timers.
    /// Used by streamer control.
    /// </summary>
    public List<(int CommandId, string Code)> ManualBlast(int count = 1)
    {
        lock (_lock)
        {
            var results = new List<(int CommandId, string Code)>();
            
            for (int i = 0; i < count; i++)
            {
                var result = _commandQueue.PollNextCommand();
                if (!result.HasValue) break;
                
                results.Add(result.Value);
                _logger.LogInformation("[MANUAL BLAST] Force-executing command #{CommandId}", result.Value.CommandId);
            }
            
            // Free all slots to allow immediate execution
            foreach (var slot in _slots)
            {
                slot.IsOccupied = false;
                // Set to a time that makes slots immediately available
                slot.LastExecutionTime = DateTime.UtcNow.AddSeconds(-DefaultSlotBlockSeconds);
            }
            
            return results;
        }
    }
    
    /// <summary>
    /// Gets the next available (unblocked) slot.
    /// Returns null if all slots are currently blocked.
    /// </summary>
    private ExecutionSlot? GetAvailableSlot()
    {
        var now = DateTime.UtcNow;
        
        foreach (var slot in _slots)
        {
            // Check if slot is not occupied or has passed its block time
            if (!slot.IsOccupied || 
                (now - slot.LastExecutionTime).TotalSeconds >= DefaultSlotBlockSeconds)
            {
                slot.IsOccupied = false; // Free it if time has passed
                return slot;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Dynamically adjusts the number of active slots based on queue depth.
    /// </summary>
    private void AdjustSlotCount()
    {
        var queueDepth = _commandQueue.GetQueueCount();
        var targetSlots = MinSlots;
        
        // Determine target slots based on queue depth
        if (queueDepth >= HighVolumeThreshold)
        {
            // High volume - scale to maximum
            targetSlots = MaxSlots;
        }
        else if (queueDepth >= LowVolumeThreshold)
        {
            // Medium volume - scale proportionally
            var ratio = (double)(queueDepth - LowVolumeThreshold) / (HighVolumeThreshold - LowVolumeThreshold);
            targetSlots = MinSlots + (int)Math.Round(ratio * (MaxSlots - MinSlots));
        }
        else
        {
            // Low volume - use minimum
            targetSlots = MinSlots;
        }
        
        // Adjust slot count
        while (_slots.Count < targetSlots)
        {
            _slots.Add(new ExecutionSlot { Id = _slots.Count + 1 });
            _logger.LogInformation("[QUEUE] Scaled UP to {Count} slots (queue depth: {QueueDepth})", 
                _slots.Count, queueDepth);
        }
        
        while (_slots.Count > targetSlots)
        {
            // Remove from the end if it's unoccupied (O(1) operation)
            var lastSlot = _slots[_slots.Count - 1];
            if (!lastSlot.IsOccupied)
            {
                _slots.RemoveAt(_slots.Count - 1);
                _logger.LogInformation("[QUEUE] Scaled DOWN to {Count} slots (queue depth: {QueueDepth})", 
                    _slots.Count, queueDepth);
            }
            else
            {
                break; // Can't scale down further without interrupting execution
            }
        }
    }
    
    /// <summary>
    /// Gets current queue status for monitoring.
    /// </summary>
    public QueueSlotStatus GetStatus()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            return new QueueSlotStatus
            {
                TotalSlots = _slots.Count,
                OccupiedSlots = _slots.Count(s => s.IsOccupied && (now - s.LastExecutionTime).TotalSeconds < DefaultSlotBlockSeconds),
                AvailableSlots = _slots.Count(s => !s.IsOccupied || 
                    (now - s.LastExecutionTime).TotalSeconds >= DefaultSlotBlockSeconds),
                Slots = _slots.Select(s => new SlotInfo
                {
                    Id = s.Id,
                    IsOccupied = s.IsOccupied && (now - s.LastExecutionTime).TotalSeconds < DefaultSlotBlockSeconds,
                    SecondsUntilAvailable = s.IsOccupied && (now - s.LastExecutionTime).TotalSeconds < DefaultSlotBlockSeconds ? 
                        DefaultSlotBlockSeconds - (now - s.LastExecutionTime).TotalSeconds : 0,
                    CurrentCommandId = s.CurrentCommandId
                }).ToList()
            };
        }
    }
    
    /// <summary>
    /// Represents an execution slot in the queue system.
    /// </summary>
    private class ExecutionSlot
    {
        public int Id { get; set; }
        public bool IsOccupied { get; set; }
        public DateTime LastExecutionTime { get; set; } = DateTime.MinValue;
        public int? CurrentCommandId { get; set; }
    }
}

/// <summary>
/// Status information about the queue slot system.
/// </summary>
public class QueueSlotStatus
{
    public int TotalSlots { get; set; }
    public int OccupiedSlots { get; set; }
    public int AvailableSlots { get; set; }
    public List<SlotInfo> Slots { get; set; } = new();
}

/// <summary>
/// Information about a single slot.
/// </summary>
public class SlotInfo
{
    public int Id { get; set; }
    public bool IsOccupied { get; set; }
    public double SecondsUntilAvailable { get; set; }
    public int? CurrentCommandId { get; set; }
}
