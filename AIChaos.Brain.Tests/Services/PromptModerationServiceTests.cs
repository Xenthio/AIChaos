using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIChaos.Brain.Tests.Services;

public class PromptModerationServiceTests
{
    [Fact]
    public void ExtractContentUrls_WithSingleUrl_ReturnsUrl()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        var prompt = "Check out this image: https://example.com/image.png";
        
        // Act
        var urls = service.ExtractContentUrls(prompt);
        
        // Assert
        Assert.Single(urls);
        Assert.Contains("https://example.com/image.png", urls);
    }
    
    [Fact]
    public void ExtractContentUrls_WithMultipleUrls_ReturnsAllUrls()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        var prompt = "Images: https://example.com/1.png and https://example.com/2.jpg";
        
        // Act
        var urls = service.ExtractContentUrls(prompt);
        
        // Assert
        Assert.Equal(2, urls.Count);
        Assert.Contains("https://example.com/1.png", urls);
        Assert.Contains("https://example.com/2.jpg", urls);
    }
    
    [Fact]
    public void ExtractContentUrls_WithNoUrls_ReturnsEmptyList()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        var prompt = "No URLs here";
        
        // Act
        var urls = service.ExtractContentUrls(prompt);
        
        // Assert
        Assert.Empty(urls);
    }
    
    [Fact]
    public void NeedsModeration_WithUrls_ReturnsTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        var prompt = "Check https://example.com/image.png";
        
        // Act
        var needsModeration = service.NeedsModeration(prompt);
        
        // Assert
        Assert.True(needsModeration);
    }
    
    [Fact]
    public void NeedsModeration_WithoutUrls_ReturnsFalse()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        var prompt = "No URLs here";
        
        // Act
        var needsModeration = service.NeedsModeration(prompt);
        
        // Assert
        Assert.False(needsModeration);
    }
    
    [Fact]
    public void AddPendingPrompt_CreatesEntryWithCorrectProperties()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        var url = "https://example.com/image.png";
        var prompt = "Test prompt";
        var source = "web";
        var author = "testuser";
        var userId = "user123";
        var commandId = 42;
        var filterReason = "External URL detected";
        
        // Act
        var entry = service.AddPendingPrompt(url, prompt, source, author, userId, commandId, filterReason);
        
        // Assert
        Assert.NotNull(entry);
        Assert.Equal(url, entry.ContentUrl);
        Assert.Equal(prompt, entry.UserPrompt);
        Assert.Equal(source, entry.Source);
        Assert.Equal(author, entry.Author);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(commandId, entry.CommandId);
        Assert.Equal(filterReason, entry.FilterReason);
        Assert.Equal(PromptModerationStatus.Pending, entry.Status);
    }
    
    [Fact]
    public void GetPendingPrompts_ReturnsOnlyPendingImages()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        var entry1 = service.AddPendingPrompt("https://example.com/1.png", "prompt1", "web", "user1", "uid1", 1);
        var entry2 = service.AddPendingPrompt("https://example.com/2.png", "prompt2", "web", "user2", "uid2", 2);
        
        // Approve one
        service.ApprovePrompt(entry1.Id);
        
        // Act
        var pending = service.GetPendingPrompts();
        
        // Assert
        Assert.Single(pending);
        Assert.Equal(entry2.Id, pending[0].Id);
    }
    
    [Fact]
    public void ApprovePrompt_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        var entry = service.AddPendingPrompt("https://example.com/image.png", "prompt", "web", "user", "uid", 1);
        
        // Act
        var approved = service.ApprovePrompt(entry.Id);
        
        // Assert
        Assert.NotNull(approved);
        Assert.Equal(PromptModerationStatus.Approved, approved.Status);
        Assert.NotNull(approved.ReviewedAt);
    }
    
    [Fact]
    public void DenyPrompt_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        var entry = service.AddPendingPrompt("https://example.com/image.png", "prompt", "web", "user", "uid", 1);
        
        // Act
        var denied = service.DenyPrompt(entry.Id);
        
        // Assert
        Assert.NotNull(denied);
        Assert.Equal(PromptModerationStatus.Denied, denied.Status);
        Assert.NotNull(denied.ReviewedAt);
    }
    
    [Fact]
    public void PendingCount_ReturnsCorrectCount()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PromptModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new PromptModerationService(mockSettings.Object, mockLogger.Object);
        
        service.AddPendingPrompt("https://example.com/1.png", "prompt1", "web", "user1", "uid1", 1);
        service.AddPendingPrompt("https://example.com/2.png", "prompt2", "web", "user2", "uid2", 2);
        var entry3 = service.AddPendingPrompt("https://example.com/3.png", "prompt3", "web", "user3", "uid3", 3);
        
        // Approve one
        service.ApprovePrompt(entry3.Id);
        
        // Act
        var count = service.PendingCount;
        
        // Assert
        Assert.Equal(2, count);
    }
}
