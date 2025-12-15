using System.Text.Json;
using System.Text.RegularExpressions;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing favourite/example prompts.
/// Allows admins to save favourite submissions that users can browse and execute.
/// Reuses the existing SavedPayload model but adds category/tagging support.
/// Each favourite is stored in a separate JSON file for easy sharing and merging.
/// </summary>
public class FavouritesService
{
    private readonly CommandQueueService _commandQueue;
    private readonly ILogger<FavouritesService> _logger;
    
    // In-memory storage for favourites with additional metadata
    private readonly List<FavouritePrompt> _favourites = new();
    private readonly object _lock = new();
    private int _nextId = 1;
    
    private readonly string _favouritesDirectory;
    private readonly string _legacyFavouritesFile;
    
    private static readonly string DefaultFavouritesDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "favourites");

    public FavouritesService(
        CommandQueueService commandQueue,
        ILogger<FavouritesService> logger)
        : this(commandQueue, logger, DefaultFavouritesDirectory)
    {
    }

    /// <summary>
    /// Constructor that allows specifying a custom favourites directory (for testing).
    /// </summary>
    public FavouritesService(
        CommandQueueService commandQueue,
        ILogger<FavouritesService> logger,
        string favouritesDirectory)
    {
        _commandQueue = commandQueue;
        _logger = logger;
        _favouritesDirectory = favouritesDirectory;
        _legacyFavouritesFile = Path.Combine(_favouritesDirectory, "favourites.json");
        LoadFavourites();
    }

    /// <summary>
    /// Adds a command to favourites.
    /// </summary>
    public FavouritePrompt? AddFavourite(
        int commandId, 
        string name, 
        string? category = null,
        string? description = null)
    {
        var command = _commandQueue.GetCommand(commandId);
        if (command == null)
        {
            _logger.LogWarning("[Favourites] Command #{CommandId} not found", commandId);
            return null;
        }
        
        lock (_lock)
        {
            var favourite = new FavouritePrompt
            {
                Id = _nextId++,
                Name = string.IsNullOrWhiteSpace(name) ? command.UserPrompt : name,
                UserPrompt = command.UserPrompt,
                ExecutionCode = command.ExecutionCode,
                UndoCode = command.UndoCode,
                Category = category ?? "General",
                Description = description,
                SavedAt = DateTime.UtcNow,
                OriginalCommandId = commandId
            };
            
            _favourites.Add(favourite);
            PersistFavourite(favourite);
            
            _logger.LogInformation("[Favourites] Added favourite '{Name}' (ID: {Id})", favourite.Name, favourite.Id);
            return favourite;
        }
    }

    /// <summary>
    /// Creates a new favourite from scratch (admin-created).
    /// </summary>
    public FavouritePrompt CreateFavourite(
        string name,
        string userPrompt,
        string executionCode,
        string undoCode,
        string? category = null,
        string? description = null)
    {
        lock (_lock)
        {
            var favourite = new FavouritePrompt
            {
                Id = _nextId++,
                Name = name,
                UserPrompt = userPrompt,
                ExecutionCode = executionCode,
                UndoCode = undoCode,
                Category = category ?? "General",
                Description = description,
                SavedAt = DateTime.UtcNow
            };
            
            _favourites.Add(favourite);
            PersistFavourite(favourite);
            
            _logger.LogInformation("[Favourites] Created favourite '{Name}' (ID: {Id})", favourite.Name, favourite.Id);
            return favourite;
        }
    }

    /// <summary>
    /// Updates an existing favourite.
    /// </summary>
    public bool UpdateFavourite(
        int id,
        string? name = null,
        string? userPrompt = null,
        string? executionCode = null,
        string? undoCode = null,
        string? category = null,
        string? description = null)
    {
        lock (_lock)
        {
            var favourite = _favourites.FirstOrDefault(f => f.Id == id);
            if (favourite == null)
                return false;
            
            // Get old filename before updating (in case name changes)
            var oldFilePath = GetFavouriteFilePath(favourite);
            
            if (name != null) favourite.Name = name;
            if (userPrompt != null) favourite.UserPrompt = userPrompt;
            if (executionCode != null) favourite.ExecutionCode = executionCode;
            if (undoCode != null) favourite.UndoCode = undoCode;
            if (category != null) favourite.Category = category;
            if (description != null) favourite.Description = description;
            
            // Get new filename after updating
            var newFilePath = GetFavouriteFilePath(favourite);
            
            // If name changed, delete old file
            if (oldFilePath != newFilePath && File.Exists(oldFilePath))
            {
                try
                {
                    File.Delete(oldFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Favourites] Failed to delete old file '{Path}' during rename", oldFilePath);
                }
            }
            
            PersistFavourite(favourite);
            return true;
        }
    }

    /// <summary>
    /// Deletes a favourite.
    /// </summary>
    public bool DeleteFavourite(int id)
    {
        lock (_lock)
        {
            var favourite = _favourites.FirstOrDefault(f => f.Id == id);
            if (favourite == null)
                return false;
            
            var filePath = GetFavouriteFilePath(favourite);
            _favourites.Remove(favourite);
            
            // Delete the individual file
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Favourites] Failed to delete file '{Path}'", filePath);
            }
            
            _logger.LogInformation("[Favourites] Deleted favourite '{Name}' (ID: {Id})", favourite.Name, id);
            return true;
        }
    }

    /// <summary>
    /// Gets all favourites.
    /// </summary>
    public List<FavouritePrompt> GetAllFavourites()
    {
        lock (_lock)
        {
            return new List<FavouritePrompt>(_favourites);
        }
    }

    /// <summary>
    /// Gets favourites by category.
    /// </summary>
    public List<FavouritePrompt> GetFavouritesByCategory(string category)
    {
        lock (_lock)
        {
            return _favourites.Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    /// <summary>
    /// Gets all distinct categories.
    /// </summary>
    public List<string> GetCategories()
    {
        lock (_lock)
        {
            return _favourites.Select(f => f.Category).Distinct().OrderBy(c => c).ToList();
        }
    }

    /// <summary>
    /// Gets a specific favourite by ID.
    /// </summary>
    public FavouritePrompt? GetFavourite(int id)
    {
        lock (_lock)
        {
            return _favourites.FirstOrDefault(f => f.Id == id);
        }
    }

    /// <summary>
    /// Executes a favourite prompt by adding it to the queue.
    /// </summary>
    public CommandEntry? ExecuteFavourite(int favouriteId, string userId, string displayName)
    {
        FavouritePrompt? favourite;
        lock (_lock)
        {
            favourite = _favourites.FirstOrDefault(f => f.Id == favouriteId);
        }
        
        if (favourite == null)
            return null;
        
        // Add command to queue
        var entry = _commandQueue.AddCommand(
            userPrompt: favourite.UserPrompt,
            executionCode: favourite.ExecutionCode,
            undoCode: favourite.UndoCode,
            source: Constants.Sources.Favourite,
            author: displayName,
            imageContext: null,
            userId: userId
        );
        
        _logger.LogInformation("[Favourites] User {User} executed favourite '{Name}' as command #{Id}", 
            displayName, favourite.Name, entry.Id);
        
        return entry;
    }

    private void LoadFavourites()
    {
        try
        {
            Directory.CreateDirectory(_favouritesDirectory);
            
            // Check for legacy single-file format and migrate if needed
            if (File.Exists(_legacyFavouritesFile))
            {
                MigrateLegacyFormat();
            }
            
            // Load all individual favourite files (excluding legacy file if still present)
            var jsonFiles = Directory.GetFiles(_favouritesDirectory, "*.json");
            var legacyFileName = Path.GetFileName(_legacyFavouritesFile);
            foreach (var filePath in jsonFiles)
            {
                // Skip legacy file if it still exists (migration may have failed)
                if (Path.GetFileName(filePath).Equals(legacyFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                try
                {
                    var json = File.ReadAllText(filePath);
                    var favourite = JsonSerializer.Deserialize<FavouritePrompt>(json);
                    if (favourite != null)
                    {
                        _favourites.Add(favourite);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Favourites] Failed to load favourite from '{Path}'", filePath);
                }
            }
            
            if (_favourites.Any())
            {
                _nextId = _favourites.Max(f => f.Id) + 1;
            }
            
            _logger.LogInformation("[Favourites] Loaded {Count} favourites from disk", _favourites.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Favourites] Failed to load favourites from disk");
        }
    }

    /// <summary>
    /// Migrates from legacy single-file format to individual files.
    /// Only deletes the legacy file if all favourites are successfully migrated.
    /// </summary>
    private void MigrateLegacyFormat()
    {
        try
        {
            var json = File.ReadAllText(_legacyFavouritesFile);
            var legacyFavourites = JsonSerializer.Deserialize<List<FavouritePrompt>>(json);
            
            if (legacyFavourites != null && legacyFavourites.Count > 0)
            {
                _logger.LogInformation("[Favourites] Migrating {Count} favourites from legacy format", legacyFavourites.Count);
                
                var migratedCount = 0;
                foreach (var favourite in legacyFavourites)
                {
                    try
                    {
                        PersistFavourite(favourite);
                        migratedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[Favourites] Failed to migrate favourite '{Name}' (ID: {Id})", favourite.Name, favourite.Id);
                    }
                }
                
                // Only delete legacy file if all favourites were migrated
                if (migratedCount == legacyFavourites.Count)
                {
                    File.Delete(_legacyFavouritesFile);
                    _logger.LogInformation("[Favourites] Migration complete, deleted legacy file");
                }
                else
                {
                    _logger.LogWarning("[Favourites] Migration incomplete ({Migrated}/{Total}), keeping legacy file for safety", 
                        migratedCount, legacyFavourites.Count);
                }
            }
            else
            {
                // Empty legacy file, just delete it
                File.Delete(_legacyFavouritesFile);
                _logger.LogInformation("[Favourites] Removed empty legacy favourites file");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Favourites] Failed to migrate legacy favourites file");
        }
    }

    /// <summary>
    /// Persists a single favourite to its individual JSON file.
    /// </summary>
    private void PersistFavourite(FavouritePrompt favourite)
    {
        try
        {
            Directory.CreateDirectory(_favouritesDirectory);
            var filePath = GetFavouriteFilePath(favourite);
            var json = JsonSerializer.Serialize(favourite, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Favourites] Failed to persist favourite '{Name}' (ID: {Id})", favourite.Name, favourite.Id);
        }
    }

    /// <summary>
    /// Gets the file path for a favourite.
    /// Uses ID and sanitized name for easy identification.
    /// </summary>
    private string GetFavouriteFilePath(FavouritePrompt favourite)
    {
        var sanitizedName = SanitizeFileName(favourite.Name);
        // Limit the sanitized name to reasonable length
        if (sanitizedName.Length > 50)
        {
            sanitizedName = sanitizedName[..50];
        }
        var fileName = $"{favourite.Id}_{sanitizedName}.json";
        return Path.Combine(_favouritesDirectory, fileName);
    }

    /// <summary>
    /// Sanitizes a string for use in a filename.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "unnamed";
        
        // Replace spaces with underscores
        var sanitized = name.Replace(' ', '_');
        
        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        sanitized = Regex.Replace(sanitized, $"[{Regex.Escape(new string(invalidChars))}]", "");
        
        // Remove consecutive underscores
        sanitized = Regex.Replace(sanitized, "_+", "_");
        
        // Trim underscores from start and end
        sanitized = sanitized.Trim('_');
        
        // If empty after sanitizing, return default
        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized.ToLowerInvariant();
    }
}

/// <summary>
/// A favourite/example prompt that users can browse and execute.
/// </summary>
public class FavouritePrompt
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public string ExecutionCode { get; set; } = "";
    public string UndoCode { get; set; } = "";
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    public int? OriginalCommandId { get; set; }
}
