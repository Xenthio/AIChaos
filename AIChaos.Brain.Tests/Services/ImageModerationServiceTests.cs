using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIChaos.Brain.Tests.Services;

public class ImageModerationServiceTests
{
    [Fact]
    public void ExtractImageUrls_WithSingleUrl_ReturnsUrl()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
        var prompt = "Check out this image: https://example.com/image.png";
        
        // Act
        var urls = service.ExtractImageUrls(prompt);
        
        // Assert
        Assert.Single(urls);
        Assert.Contains("https://example.com/image.png", urls);
    }
    
    [Fact]
    public void ExtractImageUrls_WithMultipleUrls_ReturnsAllUrls()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
        var prompt = "Images: https://example.com/1.png and https://example.com/2.jpg";
        
        // Act
        var urls = service.ExtractImageUrls(prompt);
        
        // Assert
        Assert.Equal(2, urls.Count);
        Assert.Contains("https://example.com/1.png", urls);
        Assert.Contains("https://example.com/2.jpg", urls);
    }
    
    [Fact]
    public void ExtractImageUrls_WithNoUrls_ReturnsEmptyList()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
        var prompt = "No URLs here";
        
        // Act
        var urls = service.ExtractImageUrls(prompt);
        
        // Assert
        Assert.Empty(urls);
    }
    
    [Fact]
    public void NeedsModeration_WithUrls_ReturnsTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
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
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
        var prompt = "No URLs here";
        
        // Act
        var needsModeration = service.NeedsModeration(prompt);
        
        // Assert
        Assert.False(needsModeration);
    }
    
    [Fact]
    public void AddPendingImage_CreatesEntryWithCorrectProperties()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
        var url = "https://example.com/image.png";
        var prompt = "Test prompt";
        var source = "web";
        var author = "testuser";
        var userId = "user123";
        var commandId = 42;
        
        // Act
        var entry = service.AddPendingImage(url, prompt, source, author, userId, commandId);
        
        // Assert
        Assert.NotNull(entry);
        Assert.Equal(url, entry.ImageUrl);
        Assert.Equal(prompt, entry.UserPrompt);
        Assert.Equal(source, entry.Source);
        Assert.Equal(author, entry.Author);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(commandId, entry.CommandId);
        Assert.Equal(ImageModerationStatus.Pending, entry.Status);
    }
    
    [Fact]
    public void GetPendingImages_ReturnsOnlyPendingImages()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
        var entry1 = service.AddPendingImage("https://example.com/1.png", "prompt1", "web", "user1", "uid1", 1);
        var entry2 = service.AddPendingImage("https://example.com/2.png", "prompt2", "web", "user2", "uid2", 2);
        
        // Approve one
        service.ApproveImage(entry1.Id);
        
        // Act
        var pending = service.GetPendingImages();
        
        // Assert
        Assert.Single(pending);
        Assert.Equal(entry2.Id, pending[0].Id);
    }
    
    [Fact]
    public void ApproveImage_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
        var entry = service.AddPendingImage("https://example.com/image.png", "prompt", "web", "user", "uid", 1);
        
        // Act
        var approved = service.ApproveImage(entry.Id);
        
        // Assert
        Assert.NotNull(approved);
        Assert.Equal(ImageModerationStatus.Approved, approved.Status);
        Assert.NotNull(approved.ReviewedAt);
    }
    
    [Fact]
    public void DenyImage_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
        var entry = service.AddPendingImage("https://example.com/image.png", "prompt", "web", "user", "uid", 1);
        
        // Act
        var denied = service.DenyImage(entry.Id);
        
        // Assert
        Assert.NotNull(denied);
        Assert.Equal(ImageModerationStatus.Denied, denied.Status);
        Assert.NotNull(denied.ReviewedAt);
    }
    
    [Fact]
    public void PendingCount_ReturnsCorrectCount()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ImageModerationService>>();
        var mockSettings = new Mock<SettingsService>(new Mock<ILogger<SettingsService>>().Object);
        var service = new ImageModerationService(mockSettings.Object, mockLogger.Object);
        
        service.AddPendingImage("https://example.com/1.png", "prompt1", "web", "user1", "uid1", 1);
        service.AddPendingImage("https://example.com/2.png", "prompt2", "web", "user2", "uid2", 2);
        var entry3 = service.AddPendingImage("https://example.com/3.png", "prompt3", "web", "user3", "uid3", 3);
        
        // Approve one
        service.ApproveImage(entry3.Id);
        
        // Act
        var count = service.PendingCount;
        
        // Assert
        Assert.Equal(2, count);
    }
}
