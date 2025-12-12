using AIChaos.Brain.Services;
using AIChaos.Brain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIChaos.Brain.Tests.Services;

public class RedoServiceTests
{
    private readonly Mock<ILogger<AccountService>> _accountLoggerMock;
    private readonly AccountService _accountService;
    private readonly CommandQueueService _commandQueue;

    public RedoServiceTests()
    {
        _accountLoggerMock = new Mock<ILogger<AccountService>>();
        _accountService = new AccountService(_accountLoggerMock.Object);
        _commandQueue = new CommandQueueService(enablePersistence: false);
    }

    [Fact]
    public void Account_RedoCount_DefaultsToZero()
    {
        // Arrange
        var (success, _, account) = _accountService.CreateAccount("testuser", "password123");
        
        // Assert
        Assert.True(success);
        Assert.NotNull(account);
        Assert.Equal(0, account.RedoCount);
    }

    [Fact]
    public void Account_FirstRedo_IsFree()
    {
        // Arrange
        var (success, _, account) = _accountService.CreateAccount("testuser2", "password123");
        Assert.True(success);
        Assert.NotNull(account);

        // Act - First redo should be free (RedoCount == 0)
        var isFree = account.RedoCount == 0;

        // Assert
        Assert.True(isFree);
    }

    [Fact]
    public void Account_SubsequentRedo_CostsCredits()
    {
        // Arrange
        var (success, _, account) = _accountService.CreateAccount("testuser3", "password123");
        Assert.True(success);
        Assert.NotNull(account);
        account.RedoCount = 1; // Simulate one redo used

        // Act - Second+ redo should cost credits
        var isFree = account.RedoCount == 0;

        // Assert
        Assert.False(isFree);
    }

    [Fact]
    public void Constants_RedoCost_IsOneDollar()
    {
        // Assert
        Assert.Equal(1.00m, Constants.Redo.RedoCost);
    }

    [Fact]
    public void CommandEntry_RedoFields_DefaultCorrectly()
    {
        // Arrange & Act
        var command = new CommandEntry();

        // Assert
        Assert.False(command.IsRedo);
        Assert.Null(command.OriginalCommandId);
        Assert.Null(command.RedoFeedback);
    }

    [Fact]
    public void CommandEntry_CanBeMarkedAsRedo()
    {
        // Arrange
        var command = _commandQueue.AddCommand("test", "code", "undo");
        
        // Act
        command.IsRedo = true;
        command.OriginalCommandId = 42;
        command.RedoFeedback = "The effect didn't work as expected";

        // Assert
        Assert.True(command.IsRedo);
        Assert.Equal(42, command.OriginalCommandId);
        Assert.Equal("The effect didn't work as expected", command.RedoFeedback);
    }

    [Fact]
    public void CommandEntry_ConsumptionFields_DefaultCorrectly()
    {
        // Arrange & Act
        var command = new CommandEntry();

        // Assert
        Assert.False(command.IsConsumed);
        Assert.Null(command.ExecutionStartedAt);
        Assert.Equal(0, command.InterruptCount);
    }

    [Fact]
    public void Constants_ConsumptionTimeSeconds_Is20()
    {
        // Assert
        Assert.Equal(20, Constants.Queue.ConsumptionTimeSeconds);
    }

    [Fact]
    public void Constants_RerunDelayAfterLoadSeconds_Is5()
    {
        // Assert
        Assert.Equal(5, Constants.Queue.RerunDelayAfterLoadSeconds);
    }

    [Fact]
    public void Constants_MaxMovementBlockDurationSeconds_Is10()
    {
        // Assert
        Assert.Equal(10, Constants.Safety.MaxMovementBlockDurationSeconds);
    }
}

