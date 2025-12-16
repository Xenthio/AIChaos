using System.Text.Json;
using System.Text.RegularExpressions;
using AIChaos.Brain.Models;
using Microsoft.AspNetCore.Hosting;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing favourite/example prompts.
/// Allows admins to save favourite submissions that users can browse and execute.
/// Supports both user favourites (editable) and built-in favourites (read-only, shipped with software).
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
    
    private readonly string _userFavouritesDirectory;
    private readonly string _builtInFavouritesDirectory;
    private readonly string _sourceBuiltInFavouritesDirectory; // For "Make Built-In" feature - saves to source folder
    private readonly string _legacyFavouritesFile;
    
    private static readonly string DefaultUserFavouritesDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "favourites");
    
    // Built-in favourites are loaded from BuiltInFavourites folder in the build output
    private static readonly string DefaultBuiltInFavouritesDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "BuiltInFavourites");

    public FavouritesService(
        CommandQueueService commandQueue,
        ILogger<FavouritesService> logger,
        IWebHostEnvironment environment)
        : this(commandQueue, logger, DefaultUserFavouritesDirectory, DefaultBuiltInFavouritesDirectory, 
               Path.Combine(environment.ContentRootPath, "BuiltInFavourites"))
    {
    }

    /// <summary>
    /// Constructor that allows specifying custom directories (for testing).
    /// </summary>
    public FavouritesService(
        CommandQueueService commandQueue,
        ILogger<FavouritesService> logger,
        string userFavouritesDirectory,
        string? builtInFavouritesDirectory = null,
        string? sourceBuiltInFavouritesDirectory = null)
    {
        _commandQueue = commandQueue;
        _logger = logger;
        _userFavouritesDirectory = userFavouritesDirectory;
        _builtInFavouritesDirectory = builtInFavouritesDirectory ?? DefaultBuiltInFavouritesDirectory;
        _sourceBuiltInFavouritesDirectory = sourceBuiltInFavouritesDirectory ?? _builtInFavouritesDirectory;
        _legacyFavouritesFile = Path.Combine(_userFavouritesDirectory, "favourites.json");
        LoadFavourites();
    }
    
    /// <summary>
    /// Gets the path to the built-in favourites directory (in build output, for loading).
    /// </summary>
    public string BuiltInFavouritesDirectory => _builtInFavouritesDirectory;
    
    /// <summary>
    /// Gets the path to the source built-in favourites directory (in project folder, for saving).
    /// </summary>
    public string SourceBuiltInFavouritesDirectory => _sourceBuiltInFavouritesDirectory;

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
        string? description = null,
        List<FavouriteVariation>? variations = null)
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
                SavedAt = DateTime.UtcNow,
                Variations = variations ?? new List<FavouriteVariation>()
            };
            
            _favourites.Add(favourite);
            PersistFavourite(favourite);
            
            _logger.LogInformation("[Favourites] Created favourite '{Name}' (ID: {Id}) with {VariationCount} variations", 
                favourite.Name, favourite.Id, favourite.Variations.Count);
            return favourite;
        }
    }

    /// <summary>
    /// Updates an existing favourite. Built-in favourites cannot be modified.
    /// </summary>
    public bool UpdateFavourite(
        int id,
        string? name = null,
        string? userPrompt = null,
        string? executionCode = null,
        string? undoCode = null,
        string? category = null,
        string? description = null,
        List<FavouriteVariation>? variations = null)
    {
        lock (_lock)
        {
            var favourite = _favourites.FirstOrDefault(f => f.Id == id);
            if (favourite == null)
                return false;
            
            // Built-in favourites cannot be modified
            if (favourite.IsBuiltIn)
            {
                _logger.LogWarning("[Favourites] Cannot modify built-in favourite '{Name}' (ID: {Id})", favourite.Name, id);
                return false;
            }
            
            // Get old filename before updating (in case name changes)
            var oldFilePath = GetFavouriteFilePath(favourite);
            
            if (name != null) favourite.Name = name;
            if (userPrompt != null) favourite.UserPrompt = userPrompt;
            if (executionCode != null) favourite.ExecutionCode = executionCode;
            if (undoCode != null) favourite.UndoCode = undoCode;
            if (category != null) favourite.Category = category;
            if (description != null) favourite.Description = description;
            if (variations != null) favourite.Variations = variations;
            
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
    /// Adds a variation to a favourite. Built-in favourites cannot be modified.
    /// </summary>
    public bool AddVariation(int favouriteId, string executionCode, string undoCode, string? name = null)
    {
        lock (_lock)
        {
            var favourite = _favourites.FirstOrDefault(f => f.Id == favouriteId);
            if (favourite == null)
                return false;
            
            if (favourite.IsBuiltIn)
            {
                _logger.LogWarning("[Favourites] Cannot modify built-in favourite '{Name}'", favourite.Name);
                return false;
            }
            
            favourite.Variations.Add(new FavouriteVariation
            {
                Name = name,
                ExecutionCode = executionCode,
                UndoCode = undoCode
            });
            
            PersistFavourite(favourite);
            _logger.LogInformation("[Favourites] Added variation to favourite '{Name}' (now {Count} variations)", 
                favourite.Name, favourite.Variations.Count);
            return true;
        }
    }

    /// <summary>
    /// Removes a variation from a favourite by index. Built-in favourites cannot be modified.
    /// </summary>
    public bool RemoveVariation(int favouriteId, int variationIndex)
    {
        lock (_lock)
        {
            var favourite = _favourites.FirstOrDefault(f => f.Id == favouriteId);
            if (favourite == null)
                return false;
            
            if (favourite.IsBuiltIn)
            {
                _logger.LogWarning("[Favourites] Cannot modify built-in favourite '{Name}'", favourite.Name);
                return false;
            }
            
            if (variationIndex < 0 || variationIndex >= favourite.Variations.Count)
                return false;
            
            favourite.Variations.RemoveAt(variationIndex);
            PersistFavourite(favourite);
            _logger.LogInformation("[Favourites] Removed variation {Index} from favourite '{Name}'", 
                variationIndex, favourite.Name);
            return true;
        }
    }

    /// <summary>
    /// Deletes a favourite. Built-in favourites cannot be deleted.
    /// </summary>
    public bool DeleteFavourite(int id)
    {
        lock (_lock)
        {
            var favourite = _favourites.FirstOrDefault(f => f.Id == id);
            if (favourite == null)
                return false;
            
            // Built-in favourites cannot be deleted
            if (favourite.IsBuiltIn)
            {
                _logger.LogWarning("[Favourites] Cannot delete built-in favourite '{Name}' (ID: {Id})", favourite.Name, id);
                return false;
            }
            
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
    /// Transfers a user favourite to the built-in folder.
    /// This makes it available in source control and published builds.
    /// </summary>
    public bool TransferToBuiltIn(int id)
    {
        lock (_lock)
        {
            var favourite = _favourites.FirstOrDefault(f => f.Id == id);
            if (favourite == null)
                return false;
            
            if (favourite.IsBuiltIn)
            {
                _logger.LogWarning("[Favourites] Favourite '{Name}' is already built-in", favourite.Name);
                return false;
            }
            
            // Get old user file path
            var oldFilePath = GetFavouriteFilePath(favourite, isBuiltIn: false);
            
            // Mark as built-in and persist to built-in folder
            favourite.IsBuiltIn = true;
            PersistFavourite(favourite, isBuiltIn: true);
            
            // Delete the old user file
            try
            {
                if (File.Exists(oldFilePath))
                {
                    File.Delete(oldFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Favourites] Failed to delete user file '{Path}' after transfer", oldFilePath);
            }
            
            _logger.LogInformation("[Favourites] Transferred favourite '{Name}' (ID: {Id}) to built-in", favourite.Name, id);
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
    /// If the favourite has variations, one is randomly selected.
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
        
        // Get a random variation (or main code if no variations)
        var (executionCode, undoCode) = favourite.GetRandomVariation();
        
        // Add command to queue
        var entry = _commandQueue.AddCommand(
            userPrompt: favourite.UserPrompt,
            executionCode: executionCode,
            undoCode: undoCode,
            source: Constants.Sources.Favourite,
            author: displayName,
            imageContext: null,
            userId: userId
        );
        
        var variationInfo = favourite.Variations.Count > 0 
            ? $" (random variation from {favourite.Variations.Count + 1} options)" 
            : "";
        _logger.LogInformation("[Favourites] User {User} executed favourite '{Name}'{VariationInfo} as command #{Id}", 
            displayName, favourite.Name, variationInfo, entry.Id);
        
        return entry;
    }

    private void LoadFavourites()
    {
        try
        {
            // Load built-in favourites first (read-only, shipped with software)
            LoadBuiltInFavourites();
            
            // Load user favourites (editable)
            LoadUserFavourites();
            
            if (_favourites.Any())
            {
                _nextId = _favourites.Max(f => f.Id) + 1;
            }
            
            var builtInCount = _favourites.Count(f => f.IsBuiltIn);
            var userCount = _favourites.Count(f => !f.IsBuiltIn);
            _logger.LogInformation("[Favourites] Loaded {Total} favourites ({BuiltIn} built-in, {User} user)", 
                _favourites.Count, builtInCount, userCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Favourites] Failed to load favourites from disk");
        }
    }

    private void LoadBuiltInFavourites()
    {
        try
        {
            if (!Directory.Exists(_builtInFavouritesDirectory))
            {
                _logger.LogDebug("[Favourites] Built-in favourites directory does not exist: {Path}", _builtInFavouritesDirectory);
                return;
            }
            
            var jsonFiles = Directory.GetFiles(_builtInFavouritesDirectory, "*.json");
            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var favourite = JsonSerializer.Deserialize<FavouritePrompt>(json);
                    if (favourite != null)
                    {
                        favourite.IsBuiltIn = true; // Mark as built-in
                        _favourites.Add(favourite);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Favourites] Failed to load built-in favourite from '{Path}'", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Favourites] Failed to load built-in favourites");
        }
    }

    private void LoadUserFavourites()
    {
        try
        {
            Directory.CreateDirectory(_userFavouritesDirectory);
            
            // Check for legacy single-file format and migrate if needed
            if (File.Exists(_legacyFavouritesFile))
            {
                MigrateLegacyFormat();
            }
            
            // Load all individual favourite files (excluding legacy file if still present)
            var jsonFiles = Directory.GetFiles(_userFavouritesDirectory, "*.json");
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
                        favourite.IsBuiltIn = false; // Mark as user favourite
                        _favourites.Add(favourite);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Favourites] Failed to load user favourite from '{Path}'", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Favourites] Failed to load user favourites");
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
    private void PersistFavourite(FavouritePrompt favourite, bool isBuiltIn = false)
    {
        try
        {
            // When saving to built-in, use the source directory (project folder) so it can be committed to git
            var directory = isBuiltIn ? _sourceBuiltInFavouritesDirectory : _userFavouritesDirectory;
            Directory.CreateDirectory(directory);
            var filePath = GetFavouriteFilePath(favourite, isBuiltIn, forSaving: true);
            var json = JsonSerializer.Serialize(favourite, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
            _logger.LogInformation("[Favourites] Saved favourite to '{Path}'", filePath);
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
    /// <param name="favourite">The favourite to get the path for</param>
    /// <param name="isBuiltIn">Whether this is a built-in favourite</param>
    /// <param name="forSaving">If true, uses source directory for built-in favourites (for saving to project folder)</param>
    private string GetFavouriteFilePath(FavouritePrompt favourite, bool isBuiltIn = false, bool forSaving = false)
    {
        var sanitizedName = SanitizeFileName(favourite.Name);
        // Limit the sanitized name to reasonable length
        if (sanitizedName.Length > 50)
        {
            sanitizedName = sanitizedName[..50];
        }
        var fileName = $"{favourite.Id}_{sanitizedName}.json";
        
        string directory;
        if (isBuiltIn)
        {
            // For saving, use source directory (project folder). For loading, use build output directory.
            directory = forSaving ? _sourceBuiltInFavouritesDirectory : _builtInFavouritesDirectory;
        }
        else
        {
            directory = _userFavouritesDirectory;
        }
        
        return Path.Combine(directory, fileName);
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
    
    /// <summary>
    /// Whether this favourite is built-in (shipped with the software).
    /// Built-in favourites are read-only and loaded from BuiltInFavourites folder.
    /// </summary>
    public bool IsBuiltIn { get; set; } = false;
    
    /// <summary>
    /// Alternative variations of this favourite's code that are randomly chosen when executed.
    /// If empty, the main ExecutionCode/UndoCode is used.
    /// </summary>
    public List<FavouriteVariation> Variations { get; set; } = new();
    
    /// <summary>
    /// Gets a random variation (or the main code if no variations exist).
    /// Returns the execution code and undo code as a tuple.
    /// </summary>
    public (string ExecutionCode, string UndoCode) GetRandomVariation()
    {
        if (Variations.Count == 0)
            return (ExecutionCode, UndoCode);
        
        // Include the main code as one of the options
        var random = new Random();
        var index = random.Next(Variations.Count + 1);
        
        if (index == Variations.Count)
            return (ExecutionCode, UndoCode); // Main variation
        
        return (Variations[index].ExecutionCode, Variations[index].UndoCode);
    }
}

/// <summary>
/// A variation of a favourite's code that can be randomly selected.
/// </summary>
public class FavouriteVariation
{
    /// <summary>
    /// Optional name/label for this variation (for display purposes).
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// The Lua code to execute for this variation.
    /// </summary>
    public string ExecutionCode { get; set; } = "";
    
    /// <summary>
    /// The Lua code to undo this variation.
    /// </summary>
    public string UndoCode { get; set; } = "";
}
