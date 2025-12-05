using AIChaos.Brain.Models;

namespace AIChaos.Brain.Tests.Models;

public class ApiModelsTests
{
    [Fact]
    public void CommandEntry_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var entry = new CommandEntry();
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(entry.Timestamp >= before);
        Assert.True(entry.Timestamp <= after);
        Assert.Equal(string.Empty, entry.UserPrompt);
        Assert.Equal(string.Empty, entry.ExecutionCode);
        Assert.Equal(string.Empty, entry.UndoCode);
        Assert.Null(entry.ImageContext);
        Assert.Equal("web", entry.Source);
        Assert.Equal("anonymous", entry.Author);
        Assert.Null(entry.UserId);
        Assert.Equal(CommandStatus.Pending, entry.Status);
        Assert.Null(entry.ErrorMessage);
        Assert.Null(entry.AiResponse);
        Assert.Null(entry.ExecutedAt);
    }

    [Fact]
    public void CommandEntry_SetProperties_WorksCorrectly()
    {
        // Arrange
        var entry = new CommandEntry();
        var timestamp = DateTime.UtcNow;
        var executedAt = DateTime.UtcNow.AddSeconds(5);

        // Act
        entry.Id = 42;
        entry.Timestamp = timestamp;
        entry.UserPrompt = "test prompt";
        entry.ExecutionCode = "code";
        entry.UndoCode = "undo";
        entry.ImageContext = "context";
        entry.Source = "twitch";
        entry.Author = "testuser";
        entry.UserId = "user123";
        entry.Status = CommandStatus.Executed;
        entry.ErrorMessage = "error";
        entry.AiResponse = "AI says hi";
        entry.ExecutedAt = executedAt;

        // Assert
        Assert.Equal(42, entry.Id);
        Assert.Equal(timestamp, entry.Timestamp);
        Assert.Equal("test prompt", entry.UserPrompt);
        Assert.Equal("code", entry.ExecutionCode);
        Assert.Equal("undo", entry.UndoCode);
        Assert.Equal("context", entry.ImageContext);
        Assert.Equal("twitch", entry.Source);
        Assert.Equal("testuser", entry.Author);
        Assert.Equal("user123", entry.UserId);
        Assert.Equal(CommandStatus.Executed, entry.Status);
        Assert.Equal("error", entry.ErrorMessage);
        Assert.Equal("AI says hi", entry.AiResponse);
        Assert.Equal(executedAt, entry.ExecutedAt);
    }

    [Theory]
    [InlineData(CommandStatus.Pending)]
    [InlineData(CommandStatus.PendingModeration)]
    [InlineData(CommandStatus.Queued)]
    [InlineData(CommandStatus.Executed)]
    [InlineData(CommandStatus.Undone)]
    [InlineData(CommandStatus.Failed)]
    public void CommandStatus_AllValues_CanBeSet(CommandStatus status)
    {
        // Arrange
        var entry = new CommandEntry();

        // Act
        entry.Status = status;

        // Assert
        Assert.Equal(status, entry.Status);
    }

    [Fact]
    public void TriggerRequest_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var request = new TriggerRequest();

        // Assert
        Assert.Equal(string.Empty, request.Prompt);
        Assert.Null(request.Source);
        Assert.Null(request.Author);
        Assert.Null(request.UserId);
    }

    [Fact]
    public void TriggerRequest_SetProperties_WorksCorrectly()
    {
        // Arrange
        var request = new TriggerRequest();

        // Act
        request.Prompt = "test prompt";
        request.Source = "web";
        request.Author = "testuser";
        request.UserId = "user123";

        // Assert
        Assert.Equal("test prompt", request.Prompt);
        Assert.Equal("web", request.Source);
        Assert.Equal("testuser", request.Author);
        Assert.Equal("user123", request.UserId);
    }

    [Fact]
    public void TriggerResponse_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var response = new TriggerResponse();

        // Assert
        Assert.Equal(string.Empty, response.Status);
        Assert.Null(response.Message);
        Assert.Null(response.CodePreview);
        Assert.False(response.HasUndo);
        Assert.Null(response.CommandId);
        Assert.Null(response.ContextFound);
        Assert.False(response.WasBlocked);
        Assert.Null(response.AiResponse);
    }

    [Fact]
    public void TriggerResponse_SetProperties_WorksCorrectly()
    {
        // Arrange
        var response = new TriggerResponse();

        // Act
        response.Status = "success";
        response.Message = "Command executed";
        response.CodePreview = "print('hello')";
        response.HasUndo = true;
        response.CommandId = 42;
        response.ContextFound = "image";
        response.WasBlocked = true;
        response.AiResponse = "Done!";

        // Assert
        Assert.Equal("success", response.Status);
        Assert.Equal("Command executed", response.Message);
        Assert.Equal("print('hello')", response.CodePreview);
        Assert.True(response.HasUndo);
        Assert.Equal(42, response.CommandId);
        Assert.Equal("image", response.ContextFound);
        Assert.True(response.WasBlocked);
        Assert.Equal("Done!", response.AiResponse);
    }

    [Fact]
    public void PollResponse_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var response = new PollResponse();

        // Assert
        Assert.False(response.HasCode);
        Assert.Null(response.Code);
        Assert.Null(response.CommandId);
    }

    [Fact]
    public void PollResponse_SetProperties_WorksCorrectly()
    {
        // Arrange
        var response = new PollResponse();

        // Act
        response.HasCode = true;
        response.Code = "test code";
        response.CommandId = 123;

        // Assert
        Assert.True(response.HasCode);
        Assert.Equal("test code", response.Code);
        Assert.Equal(123, response.CommandId);
    }

    [Fact]
    public void CommandIdRequest_DefaultConstructor_Initializes()
    {
        // Arrange & Act
        var request = new CommandIdRequest();

        // Assert
        Assert.Equal(0, request.CommandId);
    }

    [Fact]
    public void CommandIdRequest_SetCommandId_WorksCorrectly()
    {
        // Arrange
        var request = new CommandIdRequest();

        // Act
        request.CommandId = 456;

        // Assert
        Assert.Equal(456, request.CommandId);
    }

    [Fact]
    public void ApiResponse_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var response = new ApiResponse();

        // Assert
        Assert.Equal(string.Empty, response.Status);
        Assert.Null(response.Message);
        Assert.Null(response.CommandId);
    }

    [Fact]
    public void ApiResponse_SetProperties_WorksCorrectly()
    {
        // Arrange
        var response = new ApiResponse();

        // Act
        response.Status = "error";
        response.Message = "Something went wrong";
        response.CommandId = 789;

        // Assert
        Assert.Equal("error", response.Status);
        Assert.Equal("Something went wrong", response.Message);
        Assert.Equal(789, response.CommandId);
    }
}
