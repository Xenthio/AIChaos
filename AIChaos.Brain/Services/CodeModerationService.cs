using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing code that requires moderation before execution.
/// Similar to PromptModerationService but for filtered code patterns.
/// </summary>
public class CodeModerationService
{
    private readonly List<PendingCodeEntry> _pendingCode = new();
    private readonly object _lock = new();
    private int _nextId = 1;
    private readonly ILogger<CodeModerationService> _logger;

    public event EventHandler? PendingCodeChanged;

    public CodeModerationService(ILogger<CodeModerationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds code to the moderation queue.
    /// </summary>
    public PendingCodeEntry AddPendingCode(
        string userPrompt,
        string executionCode,
        string undoCode,
        string filterReason,
        string source,
        string author,
        string? userId,
        int? commandId = null)
    {
        lock (_lock)
        {
            var entry = new PendingCodeEntry
            {
                Id = _nextId++,
                CommandId = commandId,
                UserPrompt = userPrompt,
                ExecutionCode = executionCode,
                UndoCode = undoCode,
                FilterReason = filterReason,
                Source = source,
                Author = author,
                UserId = userId,
                SubmittedAt = DateTime.UtcNow,
                Status = CodeModerationStatus.Pending
            };

            _pendingCode.Add(entry);
            _logger.LogInformation("[CODE MODERATION] Added code to moderation queue: {Prompt} (Reason: {Reason})", 
                userPrompt, filterReason);
            
            PendingCodeChanged?.Invoke(this, EventArgs.Empty);
            return entry;
        }
    }

    /// <summary>
    /// Gets all pending code entries.
    /// </summary>
    public List<PendingCodeEntry> GetPendingCode()
    {
        lock (_lock)
        {
            return _pendingCode
                .Where(c => c.Status == CodeModerationStatus.Pending)
                .OrderBy(c => c.SubmittedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Approves code for execution.
    /// </summary>
    public PendingCodeEntry? ApproveCode(int codeId)
    {
        lock (_lock)
        {
            var entry = _pendingCode.FirstOrDefault(c => c.Id == codeId);
            if (entry != null)
            {
                entry.Status = CodeModerationStatus.Approved;
                entry.ReviewedAt = DateTime.UtcNow;
                _logger.LogInformation("[CODE MODERATION] Code #{Id} approved: {Prompt}", codeId, entry.UserPrompt);
                PendingCodeChanged?.Invoke(this, EventArgs.Empty);
            }
            return entry;
        }
    }

    /// <summary>
    /// Denies code execution.
    /// </summary>
    public PendingCodeEntry? DenyCode(int codeId)
    {
        lock (_lock)
        {
            var entry = _pendingCode.FirstOrDefault(c => c.Id == codeId);
            if (entry != null)
            {
                entry.Status = CodeModerationStatus.Denied;
                entry.ReviewedAt = DateTime.UtcNow;
                _logger.LogInformation("[CODE MODERATION] Code #{Id} denied: {Prompt}", codeId, entry.UserPrompt);
                PendingCodeChanged?.Invoke(this, EventArgs.Empty);
            }
            return entry;
        }
    }

    /// <summary>
    /// Gets a specific code entry by ID.
    /// </summary>
    public PendingCodeEntry? GetCodeEntry(int codeId)
    {
        lock (_lock)
        {
            return _pendingCode.FirstOrDefault(c => c.Id == codeId);
        }
    }

    /// <summary>
    /// Removes old approved/denied entries to prevent memory buildup.
    /// </summary>
    public void CleanupOldEntries(TimeSpan maxAge)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var removed = _pendingCode.RemoveAll(c => 
                c.Status != CodeModerationStatus.Pending && 
                c.ReviewedAt < cutoff);
            
            if (removed > 0)
            {
                _logger.LogInformation("[CODE MODERATION] Cleaned up {Count} old entries", removed);
            }
        }
    }
}
