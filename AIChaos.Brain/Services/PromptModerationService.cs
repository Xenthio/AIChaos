using System.Text.RegularExpressions;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for moderating content (URLs, images, external links) submitted in prompts.
/// Provides filtering and approval workflow for potentially unsafe content.
/// </summary>
public class PromptModerationService
{
    private readonly List<PendingPromptEntry> _pendingPrompts = new();
    private readonly SettingsService _settingsService;
    private readonly ILogger<PromptModerationService> _logger;
    private readonly object _lock = new();
    private int _nextId = 1;
    
    // Callback for processing approved prompts - set by external code
    private Func<PendingPromptEntry, Task<(bool success, string message)>>? _approvalProcessor;
    
    // Event for when pending prompts change
    public event EventHandler? PendingPromptsChanged;
    
    // Pattern to match ANY URL
    private static readonly Regex UrlPattern = new(
        @"https?://[^\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    public PromptModerationService(
        SettingsService settingsService,
        ILogger<PromptModerationService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }
    
    /// <summary>
    /// Sets the callback function that processes approved prompts (generates code and queues).
    /// This allows the approval logic to be centralized while the actual processing
    /// happens in the component layer where services like CodeGenerator are available.
    /// </summary>
    public void SetApprovalProcessor(Func<PendingPromptEntry, Task<(bool success, string message)>> processor)
    {
        _approvalProcessor = processor;
    }
    
    /// <summary>
    /// Approves and fully processes a prompt (generates code, queues command).
    /// Returns (success, message) tuple.
    /// </summary>
    public async Task<(bool success, string message)> ApproveAndProcessPromptAsync(int promptId)
    {
        PendingPromptEntry? entry;
        lock (_lock)
        {
            entry = _pendingPrompts.FirstOrDefault(i => i.Id == promptId);
            if (entry == null)
            {
                return (false, "Prompt not found");
            }
            
            entry.Status = PromptModerationStatus.Approved;
            entry.ReviewedAt = DateTime.UtcNow;
            _logger.LogInformation("[MODERATION] URL #{Id} APPROVED: {Url}", promptId, entry.ContentUrl);
        }
        
        OnPendingPromptsChanged();
        
        // Process via callback if available
        if (_approvalProcessor != null)
        {
            try
            {
                return await _approvalProcessor(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MODERATION] Error processing approved prompt #{Id}", promptId);
                return (false, $"Error processing: {ex.Message}");
            }
        }
        
        return (true, "Approved (no processor configured)");
    }
    
    private void OnPendingPromptsChanged()
    {
        PendingPromptsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Checks if the prompt contains any banned concepts.
    /// Returns tuple with ban details.
    /// </summary>
    public (bool IsBanned, bool IsHardBan, string? Category, string? Reason) CheckBannedConcepts(string prompt)
    {
        var settings = _settingsService.Settings.Safety;
        
        // Helper to check for whole words only using Regex word boundaries
        bool ContainsWholeWord(string text, string keyword)
        {
            // Escape the keyword to handle special characters safely
            // \b ensures we match "word" but not "sword" or "words"
            var pattern = $@"\b{Regex.Escape(keyword)}\b";
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
        }
        
        // Check Hard Bans first
        if (settings.HardBans != null)
        {
            foreach (var category in settings.HardBans)
            {
                if (!category.Enabled) continue;
                
                foreach (var keyword in category.Keywords)
                {
                    if (ContainsWholeWord(prompt, keyword))
                    {
                        return (true, true, category.Name, category.CustomMessage ?? $"Contains banned concept: {keyword}");
                    }
                }
            }
        }
        
        // Check Soft Bans
        if (settings.SoftBans != null)
        {
            foreach (var category in settings.SoftBans)
            {
                if (!category.Enabled) continue;
                
                foreach (var keyword in category.Keywords)
                {
                    if (ContainsWholeWord(prompt, keyword))
                    {
                        return (true, false, category.Name, "Be funnier");
                    }
                }
            }
        }
        
        return (false, false, null, null);
    }

    /// <summary>
    /// Checks if a prompt contains filtered patterns that require moderation.
    /// Returns the reason if filtered content is found, null otherwise.
    /// </summary>
    public static string? GetFilteredPatternReason(string prompt)
    {
        var filteredChecks = new Dictionary<string, string>
        {
            // URL patterns - any external link in prompt
            [@"https?://"] = "URL detected in prompt",
            
            // Discord invite patterns
            [@"discord\.gg/"] = "Discord invite link",
            [@"discord\.com/invite/"] = "Discord invite link",
            
            // Other suspicious patterns in prompts could be added here
            // For example: email addresses, phone numbers, etc.
        };

        foreach (var (pattern, reason) in filteredChecks)
        {
            if (Regex.IsMatch(prompt, pattern, RegexOptions.IgnoreCase))
            {
                return reason;
            }
        }

        return null;
    }
    
    /// <summary>
    /// Extracts ALL URLs from a prompt.
    /// </summary>
    public List<string> ExtractContentUrls(string prompt)
    {
        var urls = new HashSet<string>();
        
        // Find all URLs
        foreach (Match match in UrlPattern.Matches(prompt))
        {
            var url = match.Value.TrimEnd(')', ']', '>', ',', '.', '!', '?', ';', ':');
            urls.Add(url);
        }
        
        return urls.ToList();
    }
    
    /// <summary>
    /// Checks if a prompt contains URLs that need moderation.
    /// Uses ExtractContentUrls to find URLs in the prompt.
    /// </summary>
    public bool NeedsModeration(string prompt)
    {
        var urls = ExtractContentUrls(prompt);
        return urls.Count > 0;
    }
    
    /// <summary>
    /// Adds content (URL, image, etc.) to the moderation queue.
    /// </summary>
    public PendingPromptEntry AddPendingPrompt(string contentUrl, string userPrompt, string source, string author, string? userId, int? commandId = null, string? filterReason = null)
    {
        lock (_lock)
        {
            var entry = new PendingPromptEntry
            {
                Id = _nextId++,
                CommandId = commandId,
                ContentUrl = contentUrl,
                UserPrompt = userPrompt,
                Source = source,
                Author = author,
                UserId = userId,
                FilterReason = filterReason ?? "URL detected",
                SubmittedAt = DateTime.UtcNow,
                Status = PromptModerationStatus.Pending
            };
            
            _pendingPrompts.Add(entry);
            _logger.LogInformation("[MODERATION] URL queued for review (Command #{CommandId}, Reason: {Reason}): {Url}", 
                commandId, entry.FilterReason, contentUrl);
            
            OnPendingPromptsChanged();
            return entry;
        }
    }
    
    /// <summary>
    /// Gets all pending content awaiting moderation.
    /// </summary>
    public List<PendingPromptEntry> GetPendingPrompts()
    {
        lock (_lock)
        {
            return _pendingPrompts
                .Where(i => i.Status == PromptModerationStatus.Pending)
                .OrderBy(i => i.SubmittedAt)
                .ToList();
        }
    }
    
    /// <summary>
    /// Gets all content entries (including reviewed ones).
    /// </summary>
    public List<PendingPromptEntry> GetAllPrompts()
    {
        lock (_lock)
        {
            return new List<PendingPromptEntry>(_pendingPrompts);
        }
    }
    
    /// <summary>
    /// Approves content for processing.
    /// </summary>
    public PendingPromptEntry? ApprovePrompt(int imageId)
    {
        lock (_lock)
        {
            var entry = _pendingPrompts.FirstOrDefault(i => i.Id == imageId);
            if (entry == null) return null;
            
            entry.Status = PromptModerationStatus.Approved;
            entry.ReviewedAt = DateTime.UtcNow;
            _logger.LogInformation("[MODERATION] URL #{Id} APPROVED: {Url}", imageId, entry.ContentUrl);
            
            OnPendingPromptsChanged();
            return entry;
        }
    }
    
    /// <summary>
    /// Denies content.
    /// </summary>
    public PendingPromptEntry? DenyPrompt(int imageId)
    {
        lock (_lock)
        {
            var entry = _pendingPrompts.FirstOrDefault(i => i.Id == imageId);
            if (entry == null) return null;
            
            entry.Status = PromptModerationStatus.Denied;
            entry.ReviewedAt = DateTime.UtcNow;
            _logger.LogInformation("[MODERATION] URL #{Id} DENIED: {Url}", imageId, entry.ContentUrl);
            
            OnPendingPromptsChanged();
            return entry;
        }
    }
    
    /// <summary>
    /// Gets content by ID.
    /// </summary>
    public PendingPromptEntry? GetPrompt(int imageId)
    {
        lock (_lock)
        {
            return _pendingPrompts.FirstOrDefault(i => i.Id == imageId);
        }
    }
    
    /// <summary>
    /// Cleans up old reviewed content (older than 1 hour).
    /// </summary>
    public void CleanupOldEntries()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddHours(-1);
            _pendingPrompts.RemoveAll(i => 
                i.Status != PromptModerationStatus.Pending && 
                i.ReviewedAt.HasValue && 
                i.ReviewedAt.Value < cutoff);
        }
    }
    
    /// <summary>
    /// Gets count of pending content.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _pendingPrompts.Count(i => i.Status == PromptModerationStatus.Pending);
            }
        }
    }
}
