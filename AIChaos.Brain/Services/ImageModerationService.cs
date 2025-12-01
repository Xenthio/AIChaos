using System.Text.RegularExpressions;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for moderating images submitted in prompts.
/// </summary>
public class ImageModerationService
{
    private readonly List<PendingImageEntry> _pendingImages = new();
    private readonly SettingsService _settingsService;
    private readonly ILogger<ImageModerationService> _logger;
    private readonly object _lock = new();
    private int _nextId = 1;
    
    // Common image URL patterns
    private static readonly Regex ImageUrlPattern = new(
        @"(https?://[^\s]+\.(?:jpg|jpeg|png|gif|webp|bmp)(?:\?[^\s]*)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Alternative pattern for image hosting services
    private static readonly Regex ImageHostPattern = new(
        @"(https?://(?:i\.)?imgur\.com/[^\s]+|https?://[^\s]*discord[^\s]*\.(?:com|gg)/attachments/[^\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    public ImageModerationService(
        SettingsService settingsService,
        ILogger<ImageModerationService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }
    
    /// <summary>
    /// Extracts image URLs from a prompt.
    /// </summary>
    public List<string> ExtractImageUrls(string prompt)
    {
        var urls = new HashSet<string>();
        
        // Find direct image URLs
        foreach (Match match in ImageUrlPattern.Matches(prompt))
        {
            urls.Add(match.Value.TrimEnd(')', ']', '>'));
        }
        
        // Find image hosting URLs
        foreach (Match match in ImageHostPattern.Matches(prompt))
        {
            urls.Add(match.Value.TrimEnd(')', ']', '>'));
        }
        
        return urls.ToList();
    }
    
    /// <summary>
    /// Checks if a prompt contains images that need moderation.
    /// </summary>
    public bool NeedsModeration(string prompt)
    {
        var urls = ExtractImageUrls(prompt);
        return urls.Count > 0;
    }
    
    /// <summary>
    /// Adds an image to the moderation queue.
    /// </summary>
    public PendingImageEntry AddPendingImage(string imageUrl, string userPrompt, string source, string author, string? userId)
    {
        lock (_lock)
        {
            var entry = new PendingImageEntry
            {
                Id = _nextId++,
                ImageUrl = imageUrl,
                UserPrompt = userPrompt,
                Source = source,
                Author = author,
                UserId = userId,
                SubmittedAt = DateTime.UtcNow,
                Status = ImageModerationStatus.Pending
            };
            
            _pendingImages.Add(entry);
            _logger.LogInformation("[MODERATION] Image queued for review: {Url}", imageUrl);
            
            return entry;
        }
    }
    
    /// <summary>
    /// Gets all pending images awaiting moderation.
    /// </summary>
    public List<PendingImageEntry> GetPendingImages()
    {
        lock (_lock)
        {
            return _pendingImages
                .Where(i => i.Status == ImageModerationStatus.Pending)
                .OrderBy(i => i.SubmittedAt)
                .ToList();
        }
    }
    
    /// <summary>
    /// Gets all images (including reviewed ones).
    /// </summary>
    public List<PendingImageEntry> GetAllImages()
    {
        lock (_lock)
        {
            return new List<PendingImageEntry>(_pendingImages);
        }
    }
    
    /// <summary>
    /// Approves an image for processing.
    /// </summary>
    public PendingImageEntry? ApproveImage(int imageId)
    {
        lock (_lock)
        {
            var entry = _pendingImages.FirstOrDefault(i => i.Id == imageId);
            if (entry == null) return null;
            
            entry.Status = ImageModerationStatus.Approved;
            entry.ReviewedAt = DateTime.UtcNow;
            _logger.LogInformation("[MODERATION] Image #{Id} APPROVED: {Url}", imageId, entry.ImageUrl);
            
            return entry;
        }
    }
    
    /// <summary>
    /// Denies an image.
    /// </summary>
    public PendingImageEntry? DenyImage(int imageId)
    {
        lock (_lock)
        {
            var entry = _pendingImages.FirstOrDefault(i => i.Id == imageId);
            if (entry == null) return null;
            
            entry.Status = ImageModerationStatus.Denied;
            entry.ReviewedAt = DateTime.UtcNow;
            _logger.LogInformation("[MODERATION] Image #{Id} DENIED: {Url}", imageId, entry.ImageUrl);
            
            return entry;
        }
    }
    
    /// <summary>
    /// Gets an image by ID.
    /// </summary>
    public PendingImageEntry? GetImage(int imageId)
    {
        lock (_lock)
        {
            return _pendingImages.FirstOrDefault(i => i.Id == imageId);
        }
    }
    
    /// <summary>
    /// Cleans up old reviewed images (older than 1 hour).
    /// </summary>
    public void CleanupOldEntries()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddHours(-1);
            _pendingImages.RemoveAll(i => 
                i.Status != ImageModerationStatus.Pending && 
                i.ReviewedAt.HasValue && 
                i.ReviewedAt.Value < cutoff);
        }
    }
    
    /// <summary>
    /// Gets count of pending images.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _pendingImages.Count(i => i.Status == ImageModerationStatus.Pending);
            }
        }
    }
}
