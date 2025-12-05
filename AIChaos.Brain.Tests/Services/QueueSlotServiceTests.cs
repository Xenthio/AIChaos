using AIChaos.Brain.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIChaos.Brain.Tests.Services;

public class QueueSlotServiceTests
{
    private readonly CommandQueueService _commandQueue;
    private readonly Mock<ILogger<QueueSlotService>> _loggerMock;

    public QueueSlotServiceTests()
    {
        _commandQueue = new CommandQueueService();
        _loggerMock = new Mock<ILogger<QueueSlotService>>();
    }

    [Fact]
    public void QueueSlotService_Constructor_InitializesWithMinimumSlots()
    {
        // Arrange & Act
        var service = new QueueSlotService(_commandQueue, _loggerMock.Object);
        var status = service.GetStatus();

        // Assert
        Assert.Equal(3, status.TotalSlots); // MinSlots = 3
        Assert.Equal(3, status.AvailableSlots);
        Assert.Equal(0, status.OccupiedSlots);
    }

    [Fact]
    public void PollNextCommand_EmptyQueue_ReturnsNull()
    {
        // Arrange
        var service = new QueueSlotService(_commandQueue, _loggerMock.Object);

        // Act
        var result = service.PollNextCommand();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void PollNextCommand_WithQueuedCommand_ReturnsCommand()
    {
        // Arrange
        var commandQueue = new CommandQueueService();
        commandQueue.AddCommand("test prompt", "test code", "undo code");
        var service = new QueueSlotService(commandQueue, _loggerMock.Object);

        // Act
        var result = service.PollNextCommand();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Value.CommandId);
        Assert.Equal("test code", result.Value.Code);
    }

    [Fact]
    public void PollNextCommand_OccupiesSlot()
    {
        // Arrange
        var commandQueue = new CommandQueueService();
        commandQueue.AddCommand("test prompt", "test code", "undo code");
        var service = new QueueSlotService(commandQueue, _loggerMock.Object);

        // Act
        var result = service.PollNextCommand();
        var status = service.GetStatus();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, status.OccupiedSlots);
        Assert.Equal(2, status.AvailableSlots);
    }

    [Fact]
    public void ManualBlast_BypassesSlotTimers()
    {
        // Arrange
        var commandQueue = new CommandQueueService();
        commandQueue.AddCommand("test prompt", "test code", "undo code");
        var service = new QueueSlotService(commandQueue, _loggerMock.Object);

        // Act
        var results = service.ManualBlast(1);

        // Assert
        Assert.Single(results);
        Assert.Equal(1, results[0].CommandId);
        Assert.Equal("test code", results[0].Code);
    }

    [Fact]
    public void ManualBlast_FreesAllSlots()
    {
        // Arrange
        var commandQueue = new CommandQueueService();
        commandQueue.AddCommand("test1", "code1", "undo1");
        commandQueue.AddCommand("test2", "code2", "undo2");
        commandQueue.AddCommand("test3", "code3", "undo3");
        var service = new QueueSlotService(commandQueue, _loggerMock.Object);
        
        // Occupy some slots first
        service.PollNextCommand();
        service.PollNextCommand();

        // Act
        var results = service.ManualBlast(1);
        var status = service.GetStatus();

        // Assert
        Assert.Equal(3, status.AvailableSlots); // All slots should be available again
    }

    [Fact]
    public void ManualBlast_WithMultipleCount_ExecutesMultipleCommands()
    {
        // Arrange
        var commandQueue = new CommandQueueService();
        commandQueue.AddCommand("test1", "code1", "undo1");
        commandQueue.AddCommand("test2", "code2", "undo2");
        commandQueue.AddCommand("test3", "code3", "undo3");
        var service = new QueueSlotService(commandQueue, _loggerMock.Object);

        // Act
        var results = service.ManualBlast(3);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(1, results[0].CommandId);
        Assert.Equal(2, results[1].CommandId);
        Assert.Equal(3, results[2].CommandId);
    }

    [Fact]
    public void ManualBlast_StopsWhenQueueEmpty()
    {
        // Arrange
        var commandQueue = new CommandQueueService();
        commandQueue.AddCommand("test1", "code1", "undo1");
        commandQueue.AddCommand("test2", "code2", "undo2");
        var service = new QueueSlotService(commandQueue, _loggerMock.Object);

        // Act
        var results = service.ManualBlast(5); // Try to blast 5, but only 2 are available

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void GetStatus_ReturnsCorrectSlotInformation()
    {
        // Arrange
        var service = new QueueSlotService(_commandQueue, _loggerMock.Object);

        // Act
        var status = service.GetStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(3, status.TotalSlots);
        Assert.NotNull(status.Slots);
        Assert.Equal(3, status.Slots.Count);
        Assert.All(status.Slots, slot => {
            Assert.True(slot.Id > 0);
            Assert.False(slot.IsOccupied);
            Assert.Equal(0, slot.SecondsUntilAvailable);
        });
    }
}
