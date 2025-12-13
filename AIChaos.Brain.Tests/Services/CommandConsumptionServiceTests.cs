using AIChaos.Brain.Services;
using AIChaos.Brain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIChaos.Brain.Tests.Services;

public class CommandConsumptionServiceTests
{
    private readonly CommandQueueService _commandQueue;
    private readonly Mock<ILogger<CommandConsumptionService>> _loggerMock;
    private readonly CommandConsumptionService _service;

    public CommandConsumptionServiceTests()
    {
        _commandQueue = new CommandQueueService(enablePersistence: false);
        _loggerMock = new Mock<ILogger<CommandConsumptionService>>();
        _service = new CommandConsumptionService(_commandQueue, _loggerMock.Object, enablePersistence: false);
    }

    [Fact]
    public void StartExecution_TracksCommand()
    {
        // Arrange
        var command = _commandQueue.AddCommand("test", "code", "undo");

        // Act
        _service.StartExecution(command.Id, command.ExecutionCode);

        // Assert
        Assert.True(_service.IsExecuting(command.Id));
        Assert.Equal(1, _service.GetExecutingCount());
    }

    [Fact]
    public void CheckConsumption_BeforeThreshold_ReturnsFalse()
    {
        // Arrange
        var command = _commandQueue.AddCommand("test", "code", "undo");
        _service.StartExecution(command.Id, command.ExecutionCode);

        // Act - check immediately (before 20 seconds)
        var isConsumed = _service.CheckConsumption(command.Id);

        // Assert
        Assert.False(isConsumed);
        Assert.True(_service.IsExecuting(command.Id));
    }

    [Fact]
    public void CheckConsumption_NonExistentCommand_ReturnsFalse()
    {
        // Act
        var isConsumed = _service.CheckConsumption(999);

        // Assert
        Assert.False(isConsumed);
    }

    [Fact]
    public void HandleLevelChange_InterruptsExecutingCommands()
    {
        // Arrange
        var command = _commandQueue.AddCommand("test", "code", "undo");
        _service.StartExecution(command.Id, command.ExecutionCode);

        // Act
        var response = _service.HandleLevelChange("gm_construct", false);

        // Assert
        Assert.Equal("success", response.Status);
        Assert.Single(response.PendingReruns);
        Assert.Equal(command.Id, response.PendingReruns[0].CommandId);
        Assert.Equal(Constants.Queue.RerunDelayAfterLoadSeconds, response.PendingReruns[0].DelaySeconds);
        Assert.False(_service.IsExecuting(command.Id));
    }

    [Fact]
    public void HandleLevelChange_SaveLoad_ReportsCorrectly()
    {
        // Arrange
        var command = _commandQueue.AddCommand("test", "code", "undo");
        _service.StartExecution(command.Id, command.ExecutionCode);

        // Act
        var response = _service.HandleLevelChange("gm_flatgrass", true);

        // Assert
        Assert.Equal("success", response.Status);
        Assert.Single(response.PendingReruns);
    }

    [Fact]
    public void GetPendingReruns_ReturnsAndClearsQueue()
    {
        // Arrange
        var command = _commandQueue.AddCommand("test", "code", "undo");
        _service.StartExecution(command.Id, command.ExecutionCode);
        _service.HandleLevelChange("gm_construct", false);

        // Act
        var reruns1 = _service.GetPendingReruns();
        var reruns2 = _service.GetPendingReruns();

        // Assert
        Assert.Single(reruns1);
        Assert.Empty(reruns2); // Should be cleared after first call
    }

    [Fact]
    public void GetCurrentMap_ReturnsSetMap()
    {
        // Arrange
        _service.SetCurrentMap("gm_flatgrass");

        // Act
        var map = _service.GetCurrentMap();

        // Assert
        Assert.Equal("gm_flatgrass", map);
    }

    [Fact]
    public void HandleLevelChange_NoExecutingCommands_ReturnsEmptyReruns()
    {
        // Act
        var response = _service.HandleLevelChange("gm_construct", false);

        // Assert
        Assert.Equal("success", response.Status);
        Assert.Empty(response.PendingReruns);
    }

    [Fact]
    public void GetExecutingCount_ReturnsCorrectCount()
    {
        // Arrange
        var command1 = _commandQueue.AddCommand("test1", "code1", "undo1");
        var command2 = _commandQueue.AddCommand("test2", "code2", "undo2");

        // Act
        _service.StartExecution(command1.Id, command1.ExecutionCode);
        var count1 = _service.GetExecutingCount();
        
        _service.StartExecution(command2.Id, command2.ExecutionCode);
        var count2 = _service.GetExecutingCount();

        // Assert
        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
    }
}
