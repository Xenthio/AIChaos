using AIChaos.Brain.Services;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Tests.Services;

public class CommandQueueServiceTests
{
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
}
