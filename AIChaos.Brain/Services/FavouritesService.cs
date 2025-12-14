using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing favourite/example prompts.
/// Allows admins to save favourite submissions that users can browse and execute.
/// Reuses the existing SavedPayload model but adds category/tagging support.
/// </summary>
public class FavouritesService
{
    private readonly CommandQueueService _commandQueue;
    private readonly ILogger<FavouritesService> _logger;
    
    // In-memory storage for favourites with additional metadata
    private readonly List<FavouritePrompt> _favourites = new();
    private readonly object _lock = new();
    private int _nextId = 1;
    
    private static readonly string FavouritesDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "favourites");
    private static readonly string FavouritesFile = Path.Combine(FavouritesDirectory, "favourites.json");

    public FavouritesService(
        CommandQueueService commandQueue,
        ILogger<FavouritesService> logger)
    {
        _commandQueue = commandQueue;
        _logger = logger;
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
            PersistFavourites();
            
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
            PersistFavourites();
            
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
            
            if (name != null) favourite.Name = name;
            if (userPrompt != null) favourite.UserPrompt = userPrompt;
            if (executionCode != null) favourite.ExecutionCode = executionCode;
            if (undoCode != null) favourite.UndoCode = undoCode;
            if (category != null) favourite.Category = category;
            if (description != null) favourite.Description = description;
            
            PersistFavourites();
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
            
            _favourites.Remove(favourite);
            PersistFavourites();
            
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
            if (File.Exists(FavouritesFile))
            {
                var json = File.ReadAllText(FavouritesFile);
                var loaded = JsonSerializer.Deserialize<List<FavouritePrompt>>(json);
                if (loaded != null)
                {
                    _favourites.AddRange(loaded);
                    if (_favourites.Any())
                    {
                        _nextId = _favourites.Max(f => f.Id) + 1;
                    }
                    _logger.LogInformation("[Favourites] Loaded {Count} favourites from disk", _favourites.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Favourites] Failed to load favourites from disk");
        }
    }

    private void PersistFavourites()
    {
        try
        {
            Directory.CreateDirectory(FavouritesDirectory);
            var json = JsonSerializer.Serialize(_favourites, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(FavouritesFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Favourites] Failed to persist favourites to disk");
        }
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
