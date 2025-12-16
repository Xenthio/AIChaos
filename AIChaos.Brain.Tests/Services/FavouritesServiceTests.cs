using AIChaos.Brain.Services;
using AIChaos.Brain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIChaos.Brain.Tests.Services;

public class FavouritesServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _builtInDirectory;
    private readonly Mock<ILogger<FavouritesService>> _mockLogger;
    private readonly CommandQueueService _commandQueue;

    public FavouritesServiceTests()
    {
        // Create unique test directories for each test run
        _testDirectory = Path.Combine(Path.GetTempPath(), $"favourites_test_{Guid.NewGuid()}");
        _builtInDirectory = Path.Combine(Path.GetTempPath(), $"builtin_favourites_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_builtInDirectory);
        
        _mockLogger = new Mock<ILogger<FavouritesService>>();
        _commandQueue = new CommandQueueService(enablePersistence: false);
    }

    public void Dispose()
    {
        // Clean up test directories
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
        if (Directory.Exists(_builtInDirectory))
        {
            Directory.Delete(_builtInDirectory, true);
        }
    }

    [Fact]
    public void CreateFavourite_CreatesSeparateJsonFile()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        
        // Act
        var favourite = service.CreateFavourite(
            name: "Test Favourite",
            userPrompt: "Make everyone tiny",
            executionCode: "-- lua code",
            undoCode: "-- undo code",
            category: "Fun",
            description: "A test favourite"
        );

        // Assert
        Assert.NotNull(favourite);
        Assert.Equal("Test Favourite", favourite.Name);
        Assert.Equal(1, favourite.Id);
        
        // Check file was created
        var files = Directory.GetFiles(_testDirectory, "*.json");
        Assert.Single(files);
        Assert.Contains("1_test_favourite.json", files[0]);
    }

    [Fact]
    public void CreateFavourite_MultipleCreates_CreatesSeparateFiles()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        
        // Act
        service.CreateFavourite("First", "prompt1", "code1", "undo1");
        service.CreateFavourite("Second", "prompt2", "code2", "undo2");
        service.CreateFavourite("Third", "prompt3", "code3", "undo3");

        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.json");
        Assert.Equal(3, files.Length);
    }

    [Fact]
    public void DeleteFavourite_RemovesFile()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        var favourite = service.CreateFavourite("To Delete", "prompt", "code", "undo");
        
        // Verify file exists
        var filesBefore = Directory.GetFiles(_testDirectory, "*.json");
        Assert.Single(filesBefore);
        
        // Act
        var result = service.DeleteFavourite(favourite.Id);

        // Assert
        Assert.True(result);
        var filesAfter = Directory.GetFiles(_testDirectory, "*.json");
        Assert.Empty(filesAfter);
    }

    [Fact]
    public void UpdateFavourite_WithNameChange_RenamesFile()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        var favourite = service.CreateFavourite("Original Name", "prompt", "code", "undo");
        
        // Verify original file exists
        var originalFile = Directory.GetFiles(_testDirectory, "*.json").First();
        Assert.Contains("original_name", originalFile);
        
        // Act
        service.UpdateFavourite(favourite.Id, name: "New Name");

        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.json");
        Assert.Single(files);
        Assert.Contains("new_name", files[0]);
    }

    [Fact]
    public void SanitizeFileName_RemovesInvalidCharacters()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        
        // Act - Create favourite with characters that are invalid in filenames
        service.CreateFavourite(
            name: "Test/With\\Invalid<>Characters|\"*?",
            userPrompt: "prompt",
            executionCode: "code",
            undoCode: "undo"
        );

        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.json");
        Assert.Single(files);
        // File should exist and be readable
        Assert.True(File.Exists(files[0]));
    }

    [Fact]
    public void SanitizeFileName_TruncatesLongNames()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        var longName = new string('a', 100); // 100 character name
        
        // Act
        service.CreateFavourite(longName, "prompt", "code", "undo");

        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.json");
        Assert.Single(files);
        // Filename should be truncated to reasonable length
        var filename = Path.GetFileName(files[0]);
        Assert.True(filename.Length <= 60, $"Filename '{filename}' is too long (length: {filename.Length})");
    }

    [Fact]
    public void LoadFavourites_LoadsFromMultipleFiles()
    {
        // Arrange - Create files manually
        var fav1 = new FavouritePrompt { Id = 1, Name = "First", UserPrompt = "p1", ExecutionCode = "c1", UndoCode = "u1" };
        var fav2 = new FavouritePrompt { Id = 2, Name = "Second", UserPrompt = "p2", ExecutionCode = "c2", UndoCode = "u2" };
        
        File.WriteAllText(
            Path.Combine(_testDirectory, "1_first.json"),
            System.Text.Json.JsonSerializer.Serialize(fav1)
        );
        File.WriteAllText(
            Path.Combine(_testDirectory, "2_second.json"),
            System.Text.Json.JsonSerializer.Serialize(fav2)
        );
        
        // Act
        var service = CreateServiceWithTestDirectory();

        // Assert
        var favourites = service.GetAllFavourites();
        Assert.Equal(2, favourites.Count);
        Assert.Contains(favourites, f => f.Name == "First");
        Assert.Contains(favourites, f => f.Name == "Second");
    }

    [Fact]
    public void MigrateLegacyFormat_ConvertsToIndividualFiles()
    {
        // Arrange - Create legacy format file
        var legacyFavourites = new List<FavouritePrompt>
        {
            new() { Id = 1, Name = "Legacy One", UserPrompt = "p1", ExecutionCode = "c1", UndoCode = "u1" },
            new() { Id = 2, Name = "Legacy Two", UserPrompt = "p2", ExecutionCode = "c2", UndoCode = "u2" },
            new() { Id = 3, Name = "Legacy Three", UserPrompt = "p3", ExecutionCode = "c3", UndoCode = "u3" }
        };
        
        File.WriteAllText(
            Path.Combine(_testDirectory, "favourites.json"),
            System.Text.Json.JsonSerializer.Serialize(legacyFavourites)
        );
        
        // Act - Create service which triggers migration
        var service = CreateServiceWithTestDirectory();

        // Assert
        // Legacy file should be deleted
        Assert.False(File.Exists(Path.Combine(_testDirectory, "favourites.json")));
        
        // Individual files should exist
        var files = Directory.GetFiles(_testDirectory, "*.json");
        Assert.Equal(3, files.Length);
        
        // All favourites should be loaded
        var favourites = service.GetAllFavourites();
        Assert.Equal(3, favourites.Count);
    }

    [Fact]
    public void GetAllFavourites_ReturnsNewList()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        service.CreateFavourite("Test", "prompt", "code", "undo");
        
        // Act
        var list1 = service.GetAllFavourites();
        var list2 = service.GetAllFavourites();

        // Assert - Should be different list instances
        Assert.NotSame(list1, list2);
    }

    [Fact]
    public void NextId_ContinuesFromHighestLoadedId()
    {
        // Arrange - Create files with non-sequential IDs
        var fav1 = new FavouritePrompt { Id = 5, Name = "Five", UserPrompt = "p", ExecutionCode = "c", UndoCode = "u" };
        var fav2 = new FavouritePrompt { Id = 10, Name = "Ten", UserPrompt = "p", ExecutionCode = "c", UndoCode = "u" };
        
        File.WriteAllText(
            Path.Combine(_testDirectory, "5_five.json"),
            System.Text.Json.JsonSerializer.Serialize(fav1)
        );
        File.WriteAllText(
            Path.Combine(_testDirectory, "10_ten.json"),
            System.Text.Json.JsonSerializer.Serialize(fav2)
        );
        
        // Act
        var service = CreateServiceWithTestDirectory();
        var newFavourite = service.CreateFavourite("New", "prompt", "code", "undo");

        // Assert - New ID should be 11 (max existing + 1)
        Assert.Equal(11, newFavourite.Id);
    }

    [Fact]
    public void EmptyName_UsesFallback()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        
        // Act
        service.CreateFavourite("", "prompt", "code", "undo");

        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.json");
        Assert.Single(files);
        Assert.Contains("unnamed", files[0]);
    }

    [Fact]
    public void BuiltInFavourites_AreLoadedAndMarkedAsBuiltIn()
    {
        // Arrange - Create a built-in favourite file
        var builtInFav = new FavouritePrompt { Id = 100, Name = "Built-In Test", UserPrompt = "test", ExecutionCode = "code", UndoCode = "undo" };
        File.WriteAllText(
            Path.Combine(_builtInDirectory, "100_built_in_test.json"),
            System.Text.Json.JsonSerializer.Serialize(builtInFav)
        );
        
        // Act
        var service = CreateServiceWithTestDirectory();

        // Assert
        var favourites = service.GetAllFavourites();
        Assert.Single(favourites);
        Assert.True(favourites[0].IsBuiltIn);
        Assert.Equal("Built-In Test", favourites[0].Name);
    }

    [Fact]
    public void BuiltInFavourites_CannotBeDeleted()
    {
        // Arrange - Create a built-in favourite
        var builtInFav = new FavouritePrompt { Id = 101, Name = "Cannot Delete", UserPrompt = "test", ExecutionCode = "code", UndoCode = "undo" };
        File.WriteAllText(
            Path.Combine(_builtInDirectory, "101_cannot_delete.json"),
            System.Text.Json.JsonSerializer.Serialize(builtInFav)
        );
        var service = CreateServiceWithTestDirectory();
        
        // Act
        var result = service.DeleteFavourite(101);

        // Assert
        Assert.False(result);
        Assert.Single(service.GetAllFavourites());
    }

    [Fact]
    public void BuiltInFavourites_CannotBeModified()
    {
        // Arrange - Create a built-in favourite
        var builtInFav = new FavouritePrompt { Id = 102, Name = "Cannot Modify", UserPrompt = "test", ExecutionCode = "code", UndoCode = "undo" };
        File.WriteAllText(
            Path.Combine(_builtInDirectory, "102_cannot_modify.json"),
            System.Text.Json.JsonSerializer.Serialize(builtInFav)
        );
        var service = CreateServiceWithTestDirectory();
        
        // Act
        var result = service.UpdateFavourite(102, name: "New Name");

        // Assert
        Assert.False(result);
        Assert.Equal("Cannot Modify", service.GetFavourite(102)?.Name);
    }

    [Fact]
    public void TransferToBuiltIn_MovesFileAndMarksAsBuiltIn()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        var fav = service.CreateFavourite("User Fav", "prompt", "code", "undo");
        Assert.False(fav.IsBuiltIn);
        
        // Act
        var result = service.TransferToBuiltIn(fav.Id);

        // Assert
        Assert.True(result);
        var updatedFav = service.GetFavourite(fav.Id);
        Assert.NotNull(updatedFav);
        Assert.True(updatedFav.IsBuiltIn);
        
        // User file should be gone, built-in file should exist
        var userFiles = Directory.GetFiles(_testDirectory, "*.json");
        var builtInFiles = Directory.GetFiles(_builtInDirectory, "*.json");
        Assert.Empty(userFiles);
        Assert.Single(builtInFiles);
    }

    [Fact]
    public void BothUserAndBuiltInFavourites_LoadedTogether()
    {
        // Arrange - Create built-in favourite
        var builtInFav = new FavouritePrompt { Id = 1, Name = "Built-In", UserPrompt = "test", ExecutionCode = "code", UndoCode = "undo", IsBuiltIn = true };
        File.WriteAllText(
            Path.Combine(_builtInDirectory, "1_built_in.json"),
            System.Text.Json.JsonSerializer.Serialize(builtInFav)
        );
        
        // Create user favourite
        var userFav = new FavouritePrompt { Id = 2, Name = "User", UserPrompt = "test", ExecutionCode = "code", UndoCode = "undo" };
        File.WriteAllText(
            Path.Combine(_testDirectory, "2_user.json"),
            System.Text.Json.JsonSerializer.Serialize(userFav)
        );
        
        // Act
        var service = CreateServiceWithTestDirectory();

        // Assert
        var favourites = service.GetAllFavourites();
        Assert.Equal(2, favourites.Count);
        Assert.Single(favourites.Where(f => f.IsBuiltIn));
        Assert.Single(favourites.Where(f => !f.IsBuiltIn));
    }
    
    #region Variations Tests
    
    [Fact]
    public void CreateFavourite_WithVariations_PersistsCorrectly()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        var variations = new List<FavouriteVariation>
        {
            new() { Name = "Blue Version", ExecutionCode = "-- blue code", UndoCode = "-- blue undo" },
            new() { Name = "Red Version", ExecutionCode = "-- red code", UndoCode = "" }
        };
        
        // Act
        var favourite = service.CreateFavourite(
            "Test With Variations",
            "prompt",
            "-- main code",
            "-- main undo",
            "Fun",
            "Description",
            variations
        );
        
        // Assert
        Assert.Equal(2, favourite.Variations.Count);
        Assert.Equal("Blue Version", favourite.Variations[0].Name);
        Assert.Equal("-- blue code", favourite.Variations[0].ExecutionCode);
        Assert.Equal("Red Version", favourite.Variations[1].Name);
        
        // Verify persistence
        var files = Directory.GetFiles(_testDirectory, "*.json");
        var json = File.ReadAllText(files[0]);
        Assert.Contains("Blue Version", json);
        Assert.Contains("Red Version", json);
    }
    
    [Fact]
    public void UpdateFavourite_WithVariations_UpdatesCorrectly()
    {
        // Arrange
        var service = CreateServiceWithTestDirectory();
        var favourite = service.CreateFavourite("Original", "prompt", "code", "undo");
        
        var newVariations = new List<FavouriteVariation>
        {
            new() { Name = "Variation A", ExecutionCode = "-- var A", UndoCode = "" }
        };
        
        // Act
        var result = service.UpdateFavourite(
            favourite.Id,
            variations: newVariations
        );
        
        // Assert
        Assert.True(result);
        var updated = service.GetAllFavourites().First(f => f.Id == favourite.Id);
        Assert.Single(updated.Variations);
        Assert.Equal("Variation A", updated.Variations[0].Name);
    }
    
    [Fact]
    public void GetRandomVariation_WithNoVariations_ReturnsMainCode()
    {
        // Arrange
        var favourite = new FavouritePrompt
        {
            ExecutionCode = "-- main exec",
            UndoCode = "-- main undo"
        };
        
        // Act
        var (exec, undo) = favourite.GetRandomVariation();
        
        // Assert
        Assert.Equal("-- main exec", exec);
        Assert.Equal("-- main undo", undo);
    }
    
    [Fact]
    public void GetRandomVariation_WithVariations_ReturnsValidCode()
    {
        // Arrange
        var favourite = new FavouritePrompt
        {
            ExecutionCode = "-- main exec",
            UndoCode = "-- main undo",
            Variations = new List<FavouriteVariation>
            {
                new() { ExecutionCode = "-- var1 exec", UndoCode = "-- var1 undo" },
                new() { ExecutionCode = "-- var2 exec", UndoCode = "-- var2 undo" }
            }
        };
        
        var validExecCodes = new HashSet<string> { "-- main exec", "-- var1 exec", "-- var2 exec" };
        
        // Act - run multiple times to ensure we're getting valid results
        for (int i = 0; i < 10; i++)
        {
            var (exec, _) = favourite.GetRandomVariation();
            Assert.Contains(exec, validExecCodes);
        }
    }
    
    [Fact]
    public void FavouritePrompt_Variations_NeverNull()
    {
        // Arrange - simulate deserialization by not setting Variations
        var favourite = new FavouritePrompt
        {
            Name = "Test",
            ExecutionCode = "code"
        };
        
        // Act & Assert - should not throw and should return valid list
        Assert.NotNull(favourite.Variations);
        Assert.Empty(favourite.Variations);
    }
    
    #endregion

    /// <summary>
    /// Creates a FavouritesService that uses the test directories for storage.
    /// </summary>
    private FavouritesService CreateServiceWithTestDirectory()
    {
        // Use the same directory for both builtIn and sourceBuiltIn in tests
        return new FavouritesService(_commandQueue, _mockLogger.Object, _testDirectory, _builtInDirectory, _builtInDirectory);
    }
}
