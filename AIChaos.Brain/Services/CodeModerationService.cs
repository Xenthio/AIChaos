using System.Text.RegularExpressions;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing code that requires moderation before execution.
/// Similar to PromptModerationService but for filtered code patterns.
/// Provides pattern detection for dangerous and filtered code.
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
    /// Checks if code contains dangerous patterns that could break the game.
    /// These are always blocked, never sent to moderation.
    /// </summary>
    public static string? GetDangerousPatternReason(string code)
    {
        var dangerousChecks = new Dictionary<string, string>
        {
            [@"changelevel"] = "Map change command (changelevel)",
            [@"RunConsoleCommand.*[""']map[""']"] = "Map change via console (map)",
            [@"game\.ConsoleCommand.*[""']map\s"] = "Map change via console (map)",
            [@"game\.ConsoleCommand.*[""']changelevel"] = "Map change via console (changelevel)",
            [@"RunConsoleCommand.*[""']changelevel"] = "Map change via console (changelevel)",
            [@"RunConsoleCommand.*[""']disconnect[""']"] = "Disconnect command",
            [@"game\.ConsoleCommand.*[""']disconnect"] = "Disconnect command",
            [@":\s*Kick\s*\("] = "Player kick",
            [@"player\.Kick"] = "Player kick",
            [@"RunConsoleCommand.*[""']kill[""']"] = "Kill command (RunConsoleCommand)",
            [@"game\.ConsoleCommand.*[""']kill"] = "Kill command (game.ConsoleCommand)",
            [@"ConCommand\s*\(\s*[""']kill[""']"] = "Kill command (ConCommand)",
            [@"RunConsoleCommand.*[""']suicide[""']"] = "Suicide command (RunConsoleCommand)",
            [@"game\.ConsoleCommand.*[""']suicide"] = "Suicide command (game.ConsoleCommand)",
            [@"ConCommand\s*\(\s*[""']suicide[""']"] = "Suicide command (ConCommand)",
            [@"RunConsoleCommand.*[""']screenshot[""']"] = "Screenshot command (via screenshot concmd)",
            [@"RunConsoleCommand.*[""']jpeg[""']"] = "Screenshot command (via jpeg concmd)",
            [@"RunConsoleCommand.*[""']unbindall[""']"] = "unbindall",
            [@"game\.ConsoleCommand.*unbindall"] = "unbindall",
            [@"game\.ConsoleCommand.*suicide"] = "Suicide command",
            [@"SetHealth\s*\(\s*0\s*\)"] = "Instant death (SetHealth to 0)",
            [@"SetHealth\s*\(\s*-"] = "Instant death (negative health)",
            [@":Kill\s*\(\s*\)"] = "Instant death (Kill method)",
            [@"TakeDamage\s*\(\s*9999"] = "Extreme damage",
            [@"TakeDamage\s*\(\s*999999"] = "Extreme damage"
        };

        foreach (var (pattern, reason) in dangerousChecks)
        {
            if (Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase))
            {
                return reason;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if code contains filtered patterns that require moderation.
    /// Returns the reason if filtered content is found, null otherwise.
    /// </summary>
    public static string? GetFilteredPatternReason(string code)
    {
        var filteredChecks = new Dictionary<string, string>
        {
            // Check for specific URL opening functions first (more specific)
            [@"http\.Fetch\s*\("] = "External HTTP request (http.Fetch)",
            [@"HTTP\.Fetch\s*\("] = "External HTTP request (HTTP.Fetch)",
            [@"html:?OpenURL\s*\("] = "External URL opening (html:OpenURL)",
            [@"gui\.OpenURL\s*\("] = "External URL opening (gui.OpenURL)",
            [@"steamworks\.OpenURL\s*\("] = "External URL opening (steamworks.OpenURL)",
            
            // Check for iframes with external sources
            [@"<iframe[^>]*src\s*=\s*[""']https?://"] = "External iframe detected",
            [@"iframe.*src.*http"] = "External iframe detected",
            
            // Generic URL pattern (catches any http:// or https://)
            [@"https?://"] = "URL detected in code"
        };

        foreach (var (pattern, reason) in filteredChecks)
        {
            if (Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase))
            {
                return reason;
            }
        }

        return null;
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
