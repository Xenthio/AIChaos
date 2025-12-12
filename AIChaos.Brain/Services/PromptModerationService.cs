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
    
    private void OnPendingPromptsChanged()
    {
        PendingPromptsChanged?.Invoke(this, EventArgs.Empty);
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
    /// </summary>
    public bool NeedsModeration(string prompt)
    {
        var urls = ExtractContentUrls(prompt);
        return urls.Count > 0;
    }
    
    /// <summary>
    /// Adds content (URL, image, etc.) to the moderation queue.
    /// </summary>
    public PendingPromptEntry AddPendingPrompt(string imageUrl, string userPrompt, string source, string author, string? userId, int? commandId = null, string? filterReason = null)
    {
        lock (_lock)
        {
            var entry = new PendingPromptEntry
            {
                Id = _nextId++,
                CommandId = commandId,
                ContentUrl = imageUrl,
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
                commandId, entry.FilterReason, imageUrl);
            
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
