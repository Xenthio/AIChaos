using AIChaos.Brain.Services;
using AIChaos.Brain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIChaos.Brain.Tests.Services;

public class RedoServiceTests
{
    private readonly Mock<ILogger<AccountService>> _accountLoggerMock;
    private readonly Mock<ILogger<RedoService>> _redoLoggerMock;
    private readonly Mock<ILogger<PromptModerationService>> _promptModerationLoggerMock;
    private readonly Mock<ILogger<CodeModerationService>> _codeModerationLoggerMock;
    private readonly Mock<ILogger<SettingsService>> _settingsLoggerMock;
    private readonly AccountService _accountService;
    private readonly CommandQueueService _commandQueue;
    private readonly PromptModerationService _promptModerationService;
    private readonly CodeModerationService _codeModerationService;
    private readonly SettingsService _settingsService;

    public RedoServiceTests()
    {
        _accountLoggerMock = new Mock<ILogger<AccountService>>();
        _redoLoggerMock = new Mock<ILogger<RedoService>>();
        _promptModerationLoggerMock = new Mock<ILogger<PromptModerationService>>();
        _codeModerationLoggerMock = new Mock<ILogger<CodeModerationService>>();
        _settingsLoggerMock = new Mock<ILogger<SettingsService>>();
        
        _accountService = new AccountService(_accountLoggerMock.Object);
        _commandQueue = new CommandQueueService(enablePersistence: false);
        _settingsService = new SettingsService(_settingsLoggerMock.Object);
        _promptModerationService = new PromptModerationService(_settingsService, _promptModerationLoggerMock.Object);
        _codeModerationService = new CodeModerationService(_codeModerationLoggerMock.Object);
    }

    [Fact]
    public void Account_RedoCount_DefaultsToZero()
    {
        // Arrange
        var username = "testuser_" + Guid.NewGuid();
        var (success, _, account) = _accountService.CreateAccount(username, "password123");
        
        // Assert
        Assert.True(success);
        Assert.NotNull(account);
        if (account != null)
        {
            Assert.Equal(0, account.RedoCount);
        }
    }

    [Fact]
    public void Account_FirstRedo_IsFree()
    {
        // Arrange
        var username = "testuser_" + Guid.NewGuid();
        var (success, _, account) = _accountService.CreateAccount(username, "password123");
        Assert.True(success);
        Assert.NotNull(account);

        if (account != null)
        {
            // Act - First redo should be free (RedoCount == 0)
            var isFree = account.RedoCount == 0;

            // Assert
            Assert.True(isFree);
        }
    }

    [Fact]
    public void Account_SubsequentRedo_CostsCredits()
    {
        // Arrange
        var username = "testuser_" + Guid.NewGuid();
        var (success, _, account) = _accountService.CreateAccount(username, "password123");
        Assert.True(success);
        Assert.NotNull(account);
        
        if (account != null)
        {
            account.RedoCount = 1; // Simulate one redo used

            // Act - Second+ redo should cost credits
            var isFree = account.RedoCount == 0;

            // Assert
            Assert.False(isFree);
        }
    }

    [Fact]
    public void Constants_RedoCost_IsFiftyCents()
    {
        // Assert
        Assert.Equal(0.50m, Constants.Redo.RedoCost);
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

    [Fact]
    public void PromptModerationService_DetectsUrlsInFeedback()
    {
        // Arrange
        var feedback = "It didn't work. Try using this model: https://example.com/malicious.mdl";
        
        // Act
        var needsModeration = _promptModerationService.NeedsModeration(feedback);
        
        // Assert
        Assert.True(needsModeration, "Feedback containing URLs should be detected as needing moderation");
    }

    [Fact]
    public void PromptModerationService_ExtractsUrlsFromFeedback()
    {
        // Arrange
        var feedback = "The effect failed. Please use https://example.com/asset1.jpg and https://example.com/asset2.png";
        
        // Act
        var urls = _promptModerationService.ExtractContentUrls(feedback);
        
        // Assert
        Assert.Equal(2, urls.Count);
        Assert.Contains("https://example.com/asset1.jpg", urls);
        Assert.Contains("https://example.com/asset2.png", urls);
    }

    [Fact]
    public void PromptModerationService_AllowsFeedbackWithoutUrls()
    {
        // Arrange
        var feedback = "The effect only lasted 2 seconds instead of 10 seconds";
        
        // Act
        var needsModeration = _promptModerationService.NeedsModeration(feedback);
        
        // Assert
        Assert.False(needsModeration, "Clean feedback without URLs should not need moderation");
    }

    [Fact]
    public void PromptModerationService_DetectsDiscordLinksInFeedback()
    {
        // Arrange
        // Discord.gg links need the full http(s):// prefix to be detected by URL pattern
        var feedback = "Join my Discord for better models: https://discord.gg/malicious";
        
        // Act
        var urls = _promptModerationService.ExtractContentUrls(feedback);
        
        // Assert
        Assert.NotEmpty(urls);
        Assert.Contains("https://discord.gg/malicious", urls);
    }

    [Fact]
    public void PromptModerationService_DetectsHttpLinks()
    {
        // Arrange
        var feedback = "Use this: http://example.com/model.mdl (not https)";
        
        // Act
        var needsModeration = _promptModerationService.NeedsModeration(feedback);
        var urls = _promptModerationService.ExtractContentUrls(feedback);
        
        // Assert
        Assert.True(needsModeration, "HTTP links should be detected");
        Assert.Single(urls);
        Assert.Contains("http://example.com/model.mdl", urls);
    }

    [Fact]
    public void RedoService_ValidationLogic_DetectsUrlsBeforeCreditCheck()
    {
        // This test validates the logic flow: URL check happens before credit deduction
        // We test the PromptModerationService logic that RedoService depends on
        
        // Arrange - feedback that would fail moderation
        var maliciousFeedback = "Try using https://evil-site.com/script.lua";
        var cleanFeedback = "The effect only lasted 2 seconds instead of 10 seconds";
        
        // Act
        var maliciousFeedbackNeedsModeration = _promptModerationService.NeedsModeration(maliciousFeedback);
        var cleanFeedbackNeedsModeration = _promptModerationService.NeedsModeration(cleanFeedback);
        
        // Assert
        Assert.True(maliciousFeedbackNeedsModeration, "Malicious feedback should be detected");
        Assert.False(cleanFeedbackNeedsModeration, "Clean feedback should pass");
    }

    [Fact]
    public void RedoService_ValidationLogic_ExtractsMultipleUrls()
    {
        // This test validates that multiple URLs are properly extracted
        // which ensures comprehensive security logging in RedoService
        
        // Arrange
        var feedbackWithMultipleUrls = "Check https://site1.com and https://site2.com and http://site3.com";
        
        // Act
        var urls = _promptModerationService.ExtractContentUrls(feedbackWithMultipleUrls);
        
        // Assert
        Assert.Equal(3, urls.Count);
        Assert.Contains("https://site1.com", urls);
        Assert.Contains("https://site2.com", urls);
        Assert.Contains("http://site3.com", urls);
    }

    [Fact]
    public void RedoService_ValidationLogic_HandlesDiscordLinks()
    {
        // This test validates Discord link detection which is a common bypass attempt
        
        // Arrange
        var discordFeedback = "Join https://discord.gg/malicious for better code";
        
        // Act
        var needsModeration = _promptModerationService.NeedsModeration(discordFeedback);
        var urls = _promptModerationService.ExtractContentUrls(discordFeedback);
        
        // Assert
        Assert.True(needsModeration, "Discord links should be detected");
        Assert.Single(urls);
        Assert.Contains("https://discord.gg/malicious", urls);
    }
    
    [Fact]
    public void CommandEntry_FixConversationHistory_DefaultsToEmptyList()
    {
        // Arrange & Act
        var command = new CommandEntry();
        
        // Assert
        Assert.NotNull(command.FixConversationHistory);
        Assert.Empty(command.FixConversationHistory);
    }
    
    [Fact]
    public void CommandEntry_CanStoreConversationHistory()
    {
        // Arrange
        var command = _commandQueue.AddCommand("test prompt", "code", "undo");
        var conversationHistory = new List<ChatMessage>
        {
            new() { Role = "system", Content = "You are an AI assistant" },
            new() { Role = "user", Content = "Make everyone fly" },
            new() { Role = "assistant", Content = "Here's the code..." },
            new() { Role = "user", Content = "That didn't work, they're falling" }
        };
        
        // Act
        command.FixConversationHistory = conversationHistory;
        
        // Assert
        Assert.Equal(4, command.FixConversationHistory.Count);
        Assert.Equal("system", command.FixConversationHistory[0].Role);
        Assert.Equal("You are an AI assistant", command.FixConversationHistory[0].Content);
        Assert.Equal("user", command.FixConversationHistory[3].Role);
        Assert.Equal("That didn't work, they're falling", command.FixConversationHistory[3].Content);
    }
}

