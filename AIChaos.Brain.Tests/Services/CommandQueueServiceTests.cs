using AIChaos.Brain.Services;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Tests.Services;

public class CommandQueueServiceTests : IDisposable
{
    private static readonly string TestSavedPayloadsDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "saved_payloads");

    public CommandQueueServiceTests()
    {
        // Clean up before each test
        CleanupSavedPayloads();
    }

    public void Dispose()
    {
        // Clean up after each test
        CleanupSavedPayloads();
    }

    private void CleanupSavedPayloads()
    {
        try
        {
            if (Directory.Exists(TestSavedPayloadsDirectory))
            {
                Directory.Delete(TestSavedPayloadsDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void CommandQueueService_Constructor_InitializesEmpty()
    {
        // Arrange & Act
        var service = new CommandQueueService();

        // Assert
        Assert.Equal(0, service.GetQueueCount());
        Assert.Empty(service.GetHistory());
    }

    [Fact]
    public void AddCommand_AddsToQueueAndHistory()
    {
        // Arrange
        var service = new CommandQueueService();

        // Act
        var entry = service.AddCommand("test prompt", "execution code", "undo code");

        // Assert
        Assert.Equal(1, entry.Id);
        Assert.Equal(1, service.GetQueueCount());
        Assert.Single(service.GetHistory());
        Assert.Equal("test prompt", entry.UserPrompt);
        Assert.Equal("execution code", entry.ExecutionCode);
        Assert.Equal("undo code", entry.UndoCode);
        Assert.Equal(CommandStatus.Queued, entry.Status);
    }

    [Fact]
    public void AddCommand_WithAllParameters_SetsCorrectValues()
    {
        // Arrange
        var service = new CommandQueueService();

        // Act
        var entry = service.AddCommand(
            "prompt", 
            "exec", 
            "undo", 
            "twitch", 
            "testuser",
            "image_context",
            "user123",
            "AI response text"
        );

        // Assert
        Assert.Equal("prompt", entry.UserPrompt);
        Assert.Equal("exec", entry.ExecutionCode);
        Assert.Equal("undo", entry.UndoCode);
        Assert.Equal("twitch", entry.Source);
        Assert.Equal("testuser", entry.Author);
        Assert.Equal("image_context", entry.ImageContext);
        Assert.Equal("user123", entry.UserId);
        Assert.Equal("AI response text", entry.AiResponse);
    }

    [Fact]
    public void AddCommand_WithoutQueueing_AddsToHistoryOnly()
    {
        // Arrange
        var service = new CommandQueueService();

        // Act
        var entry = service.AddCommand(
            "prompt", "exec", "undo", "web", "anon", null, null, null, 
            queueForExecution: false
        );

        // Assert
        Assert.Equal(0, service.GetQueueCount());
        Assert.Single(service.GetHistory());
        Assert.Equal(CommandStatus.Queued, entry.Status);
    }

    [Fact]
    public void AddCommandWithStatus_SetsSpecificStatus()
    {
        // Arrange
        var service = new CommandQueueService();

        // Act
        var entry = service.AddCommandWithStatus(
            "prompt", "exec", "undo", "web", "anon", null, null, null,
            CommandStatus.Executed
        );

        // Assert
        Assert.Equal(CommandStatus.Executed, entry.Status);
        Assert.Equal(0, service.GetQueueCount());
        Assert.Single(service.GetHistory());
    }

    [Fact]
    public void PollNextCommand_EmptyQueue_ReturnsNull()
    {
        // Arrange
        var service = new CommandQueueService();

        // Act
        var result = service.PollNextCommand();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void PollNextCommand_WithQueuedCommand_ReturnsCommand()
    {
        // Arrange
        var service = new CommandQueueService();
        service.AddCommand("prompt", "code", "undo");

        // Act
        var result = service.PollNextCommand();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Value.CommandId);
        Assert.Equal("code", result.Value.Code);
    }

    [Fact]
    public void PollNextCommand_RemovesFromQueue()
    {
        // Arrange
        var service = new CommandQueueService();
        service.AddCommand("prompt", "code", "undo");

        // Act
        var result1 = service.PollNextCommand();
        var result2 = service.PollNextCommand();

        // Assert
        Assert.NotNull(result1);
        Assert.Null(result2);
        Assert.Equal(0, service.GetQueueCount());
    }

    [Fact]
    public void PollNextCommand_FIFO_Order()
    {
        // Arrange
        var service = new CommandQueueService();
        service.AddCommand("first", "code1", "undo1");
        service.AddCommand("second", "code2", "undo2");
        service.AddCommand("third", "code3", "undo3");

        // Act
        var result1 = service.PollNextCommand();
        var result2 = service.PollNextCommand();
        var result3 = service.PollNextCommand();

        // Assert
        Assert.Equal("code1", result1?.Code);
        Assert.Equal("code2", result2?.Code);
        Assert.Equal("code3", result3?.Code);
    }

    [Fact]
    public void GetHistory_ReturnsAllEntries()
    {
        // Arrange
        var service = new CommandQueueService();
        service.AddCommand("first", "code1", "undo1");
        service.AddCommand("second", "code2", "undo2");

        // Act
        var history = service.GetHistory();

        // Assert
        Assert.Equal(2, history.Count);
        Assert.Equal("first", history[0].UserPrompt);
        Assert.Equal("second", history[1].UserPrompt);
    }

    [Fact]
    public void GetCommand_ReturnsCorrectCommand()
    {
        // Arrange
        var service = new CommandQueueService();
        var entry1 = service.AddCommand("first", "code1", "undo1");
        var entry2 = service.AddCommand("second", "code2", "undo2");

        // Act
        var retrieved = service.GetCommand(entry2.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(entry2.Id, retrieved.Id);
        Assert.Equal("second", retrieved.UserPrompt);
    }

    [Fact]
    public void GetCommand_NonExistent_ReturnsNull()
    {
        // Arrange
        var service = new CommandQueueService();

        // Act
        var result = service.GetCommand(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Preferences_DefaultValues()
    {
        // Arrange & Act
        var service = new CommandQueueService();

        // Assert
        Assert.NotNull(service.Preferences);
        Assert.True(service.Preferences.MaxHistoryLength > 0);
    }

    [Fact]
    public void HistoryChanged_EventFires_WhenCommandAdded()
    {
        // Arrange
        var service = new CommandQueueService();
        var eventFired = false;
        service.HistoryChanged += (sender, args) => eventFired = true;

        // Act
        service.AddCommand("test", "code", "undo");

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void AutoIncrementId_WorksCorrectly()
    {
        // Arrange
        var service = new CommandQueueService();

        // Act
        var entry1 = service.AddCommand("first", "code1", "undo1");
        var entry2 = service.AddCommand("second", "code2", "undo2");
        var entry3 = service.AddCommand("third", "code3", "undo3");

        // Assert
        Assert.Equal(1, entry1.Id);
        Assert.Equal(2, entry2.Id);
        Assert.Equal(3, entry3.Id);
    }

    [Fact]
    public void SavePayload_CreatesPayload()
    {
        // Arrange
        var service = new CommandQueueService();
        var entry = service.AddCommand("test prompt", "execution code", "undo code");

        // Act
        var payload = service.SavePayload(entry, "Test Payload");

        // Assert
        Assert.NotNull(payload);
        Assert.Equal("Test Payload", payload.Name);
        Assert.Equal("test prompt", payload.UserPrompt);
        Assert.Equal("execution code", payload.ExecutionCode);
        Assert.Equal("undo code", payload.UndoCode);
    }

    [Fact]
    public void GetSavedPayloads_ReturnsAllPayloads()
    {
        // Arrange
        var service = new CommandQueueService();
        var entry1 = service.AddCommand("first", "code1", "undo1");
        var entry2 = service.AddCommand("second", "code2", "undo2");

        // Act
        service.SavePayload(entry1, "Payload 1");
        service.SavePayload(entry2, "Payload 2");
        var payloads = service.GetSavedPayloads();

        // Assert
        Assert.Equal(2, payloads.Count);
        Assert.Contains(payloads, p => p.Name == "Payload 1");
        Assert.Contains(payloads, p => p.Name == "Payload 2");
    }

    [Fact]
    public void LoadSavedPayloads_MigratesFromOldFormat()
    {
        // Arrange - Create old format file
        Directory.CreateDirectory(TestSavedPayloadsDirectory);
        var oldFormatFile = Path.Combine(TestSavedPayloadsDirectory, "payloads.json");
        var oldPayloads = new List<SavedPayload>
        {
            new SavedPayload 
            { 
                Id = 1, 
                Name = "Old Payload 1", 
                UserPrompt = "prompt1", 
                ExecutionCode = "code1", 
                UndoCode = "undo1" 
            },
            new SavedPayload 
            { 
                Id = 2, 
                Name = "Old Payload 2", 
                UserPrompt = "prompt2", 
                ExecutionCode = "code2", 
                UndoCode = "undo2" 
            }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(oldPayloads, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(oldFormatFile, json);

        // Act - Create service which should trigger migration
        var service = new CommandQueueService();
        var payloads = service.GetSavedPayloads();

        // Assert
        Assert.Equal(2, payloads.Count);
        Assert.Contains(payloads, p => p.Name == "Old Payload 1");
        Assert.Contains(payloads, p => p.Name == "Old Payload 2");
        
        // Verify old file is deleted
        Assert.False(File.Exists(oldFormatFile));
        
        // Verify individual files were created
        var files = Directory.GetFiles(TestSavedPayloadsDirectory, "*.json");
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void SavedPayload_FilesHaveSanitizedNames()
    {
        // Arrange
        var service = new CommandQueueService();
        var entry = service.AddCommand("Test / Invalid : Chars", "code", "undo");

        // Act
        var payload = service.SavePayload(entry, "Test / Invalid : Chars");

        // Assert
        var files = Directory.GetFiles(TestSavedPayloadsDirectory, "*.json");
        Assert.Single(files);
        
        // File should have sanitized name (special chars replaced with _)
        var fileName = Path.GetFileName(files[0]);
        Assert.DoesNotContain("/", fileName);
        Assert.DoesNotContain(":", fileName);
        Assert.Contains("_", fileName);
    }

    [Fact]
    public void DeletePayload_RemovesPayload()
    {
        // Arrange
        var service = new CommandQueueService();
        var entry = service.AddCommand("test", "code", "undo");
        var payload = service.SavePayload(entry, "Test Payload");

        // Act
        var result = service.DeletePayload(payload.Id);
        var payloads = service.GetSavedPayloads();

        // Assert
        Assert.True(result);
        Assert.Empty(payloads);
    }

    [Fact]
    public void DeletePayload_NonExistent_ReturnsFalse()
    {
        // Arrange
        var service = new CommandQueueService();

        // Act
        var result = service.DeletePayload(999);

        // Assert
        Assert.False(result);
    }
}
