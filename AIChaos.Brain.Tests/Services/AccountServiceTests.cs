using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIChaos.Brain.Tests.Services;

public class AccountServiceTests
{
    [Fact]
    public void GetOrCreateAnonymousAccount_CreatesAccount_WhenNotExists()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);

        // Act
        var account = service.GetOrCreateAnonymousAccount();

        // Assert
        Assert.NotNull(account);
        Assert.Equal("anonymous-default-user", account.Id);
        Assert.Equal("anonymous", account.Username);
        Assert.Equal("Anonymous User", account.DisplayName);
        Assert.Equal(decimal.MaxValue, account.CreditBalance);
        Assert.Equal(UserRole.User, account.Role);
    }

    [Fact]
    public void GetOrCreateAnonymousAccount_ReturnsSameAccount_WhenCalledMultipleTimes()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);

        // Act
        var account1 = service.GetOrCreateAnonymousAccount();
        var account2 = service.GetOrCreateAnonymousAccount();

        // Assert
        Assert.Same(account1, account2);
        Assert.Equal("anonymous-default-user", account1.Id);
        Assert.Equal("anonymous-default-user", account2.Id);
    }

    [Fact]
    public void GetOrCreateAnonymousAccount_HasUnlimitedCredits()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);

        // Act
        var account = service.GetOrCreateAnonymousAccount();

        // Assert
        Assert.Equal(decimal.MaxValue, account.CreditBalance);
    }
    
    [Fact]
    public void GetAccountsWithIncorrectYouTubeIds_DetectsGoogleIds()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);
        
        // Create accounts with different ID types
        var (success1, _, account1) = service.CreateAccount("user1_" + Guid.NewGuid(), "password123", "User One");
        var (success2, _, account2) = service.CreateAccount("user2_" + Guid.NewGuid(), "password456", "User Two");
        var (success3, _, account3) = service.CreateAccount("user3_" + Guid.NewGuid(), "password789", "User Three");
        
        Assert.NotNull(account1);
        Assert.NotNull(account2);
        Assert.NotNull(account3);
        
        // Generate unique IDs to avoid conflicts
        var googleId1 = DateTime.UtcNow.Ticks.ToString().PadLeft(21, '0').Substring(0, 21); // Google ID (21 digits)
        var youtubeId = "UC" + Guid.NewGuid().ToString("N").Substring(0, 22); // Valid YouTube Channel ID
        var googleId3 = (DateTime.UtcNow.Ticks + 1).ToString().PadLeft(21, '0').Substring(0, 21); // Another Google ID
        
        // Link account1 with a Google ID (all digits, 21 chars)
        var link1 = service.LinkYouTubeChannel(account1.Id, googleId1, null);
        Assert.True(link1, "Failed to link account1 with Google ID");
        
        // Link account2 with a proper YouTube Channel ID (starts with UC, 24 chars)
        var link2 = service.LinkYouTubeChannel(account2.Id, youtubeId, null);
        Assert.True(link2, "Failed to link account2 with YouTube ID");
        
        // Link account3 with another Google ID format
        var link3 = service.LinkYouTubeChannel(account3.Id, googleId3, null);
        Assert.True(link3, "Failed to link account3 with Google ID");
        
        // Act
        var accountsWithIssues = service.GetAccountsWithIncorrectYouTubeIds();
        
        // Assert
        Assert.True(accountsWithIssues.Count >= 2, $"Expected at least 2 accounts with issues, got {accountsWithIssues.Count}");
        Assert.Contains(accountsWithIssues, a => a.Id == account1.Id);
        Assert.Contains(accountsWithIssues, a => a.Id == account3.Id);
        Assert.DoesNotContain(accountsWithIssues, a => a.Id == account2.Id);
    }
    
    [Fact]
    public void UnlinkYouTubeChannel_RemovesLink()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);
        
        var (success, _, account) = service.CreateAccount("testuser_" + Guid.NewGuid(), "password123", "Test User");
        Assert.NotNull(account);
        
        var channelId = "UC" + Guid.NewGuid().ToString("N").Substring(0, 22);  // Generate unique channel ID
        var linkSuccess = service.LinkYouTubeChannel(account.Id, channelId, null);
        Assert.True(linkSuccess);
        
        // Get fresh account to verify it's linked
        var linkedAccount = service.GetAccountById(account.Id);
        Assert.NotNull(linkedAccount);
        Assert.Equal(channelId, linkedAccount.LinkedYouTubeChannelId);
        
        // Act
        var unlinkSuccess = service.UnlinkYouTubeChannel(account.Id);
        
        // Assert
        Assert.True(unlinkSuccess);
        var updatedAccount = service.GetAccountById(account.Id);
        Assert.NotNull(updatedAccount);
        Assert.Null(updatedAccount.LinkedYouTubeChannelId);
        
        // Verify the account is no longer found by channel ID
        var accountByChannel = service.GetAccountByYouTubeChannel(channelId);
        Assert.Null(accountByChannel);
    }
    
    [Fact]
    public void UnlinkAccountsWithIncorrectYouTubeIds_UnlinksOnlyIncorrectOnes()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);
        
        // Create accounts with unique usernames
        var user1 = "user1_" + Guid.NewGuid();
        var user2 = "user2_" + Guid.NewGuid();
        var user3 = "user3_" + Guid.NewGuid();
        
        var (_, _, account1) = service.CreateAccount(user1, "password123", "User One");
        var (_, _, account2) = service.CreateAccount(user2, "password456", "User Two");
        var (_, _, account3) = service.CreateAccount(user3, "password789", "User Three");
        
        Assert.NotNull(account1);
        Assert.NotNull(account2);
        Assert.NotNull(account3);
        
        // Link with different ID types
        var googleId1 = "123456789012345678901"; // Google ID
        var youtubeId = "UC" + Guid.NewGuid().ToString("N").Substring(0, 22); // Valid YouTube Channel ID
        var googleId3 = "987654321098765432109"; // Google ID
        
        service.LinkYouTubeChannel(account1.Id, googleId1, null);
        service.LinkYouTubeChannel(account2.Id, youtubeId, null);
        service.LinkYouTubeChannel(account3.Id, googleId3, null);
        
        // Count how many incorrect IDs exist before unlinking
        var incorrectBefore = service.GetAccountsWithIncorrectYouTubeIds();
        var ourIncorrectCount = incorrectBefore.Count(a => a.Id == account1.Id || a.Id == account3.Id);
        
        // Act
        var unlinkedCount = service.UnlinkAccountsWithIncorrectYouTubeIds();
        
        // Assert - at least our 2 accounts should be unlinked
        Assert.True(unlinkedCount >= ourIncorrectCount, $"Expected at least {ourIncorrectCount} unlinked, got {unlinkedCount}");
        
        var updatedAccount1 = service.GetAccountById(account1.Id);
        var updatedAccount2 = service.GetAccountById(account2.Id);
        var updatedAccount3 = service.GetAccountById(account3.Id);
        
        Assert.NotNull(updatedAccount1);
        Assert.NotNull(updatedAccount2);
        Assert.NotNull(updatedAccount3);
        
        Assert.Null(updatedAccount1.LinkedYouTubeChannelId); // Unlinked
        Assert.Equal(youtubeId, updatedAccount2.LinkedYouTubeChannelId); // Still linked
        Assert.Null(updatedAccount3.LinkedYouTubeChannelId); // Unlinked
    }
    
    [Fact]
    public void ManuallyLinkPendingCredits_TransfersCreditsAndLinks()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);
        
        var (_, _, account) = service.CreateAccount("testuser_" + Guid.NewGuid(), "password123", "Test User");
        Assert.NotNull(account);
        
        var channelId = "UC" + Guid.NewGuid().ToString("N").Substring(0, 22);
        
        // Add pending credits for this channel
        service.AddCreditsToChannel(channelId, 5.00m, "Test User", "Test donation");
        service.AddCreditsToChannel(channelId, 2.50m, "Test User", "Another donation");
        
        // Verify pending credits exist
        var pendingAmount = service.GetPendingCreditsForChannel(channelId);
        Assert.Equal(7.50m, pendingAmount);
        
        var initialBalance = account.CreditBalance;
        
        // Act
        var success = service.ManuallyLinkPendingCredits(account.Id, channelId);
        
        // Assert
        Assert.True(success);
        
        var updatedAccount = service.GetAccountById(account.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal(initialBalance + 7.50m, updatedAccount.CreditBalance);
        Assert.Equal(channelId, updatedAccount.LinkedYouTubeChannelId);
        
        // Verify pending credits are cleared
        var remainingPending = service.GetPendingCreditsForChannel(channelId);
        Assert.Equal(0m, remainingPending);
    }

    [Fact]
    public void DeductCredits_WithSufficientBalance_DeductsCreditsAndReturnsTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);
        
        var (_, _, account) = service.CreateAccount("testuser_" + Guid.NewGuid(), "password123", "Test User");
        Assert.NotNull(account);
        
        // Add initial credits
        service.AddCredits(account.Id, 10.00m);
        var initialBalance = service.GetAccountById(account.Id)?.CreditBalance ?? 0;
        
        // Act
        var deductSuccess = service.DeductCredits(account.Id, 1.00m);
        
        // Assert
        Assert.True(deductSuccess);
        var updatedAccount = service.GetAccountById(account.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal(initialBalance - 1.00m, updatedAccount.CreditBalance);
        Assert.Equal(1.00m, updatedAccount.TotalSpent);
    }

    [Fact]
    public void DeductCredits_WithInsufficientBalance_ReturnsFalseAndDoesNotDeduct()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);
        
        var (_, _, account) = service.CreateAccount("testuser_" + Guid.NewGuid(), "password123", "Test User");
        Assert.NotNull(account);
        
        // Add only 0.50 credits
        service.AddCredits(account.Id, 0.50m);
        var initialBalance = service.GetAccountById(account.Id)?.CreditBalance ?? 0;
        
        // Act - Try to deduct 1.00 (more than available)
        var deductSuccess = service.DeductCredits(account.Id, 1.00m);
        
        // Assert
        Assert.False(deductSuccess);
        var updatedAccount = service.GetAccountById(account.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal(initialBalance, updatedAccount.CreditBalance); // Balance unchanged
        Assert.Equal(0m, updatedAccount.TotalSpent); // No spending recorded
    }

    [Fact]
    public void DeductCredits_ForNonExistentAccount_ReturnsFalse()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);
        
        // Act - Try to deduct from non-existent account
        var deductSuccess = service.DeductCredits("non-existent-id", 1.00m);
        
        // Assert
        Assert.False(deductSuccess);
    }

    [Fact]
    public void DeductCredits_MultipleDeductions_TracksCorrectTotalSpent()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AccountService>>();
        var service = new AccountService(mockLogger.Object);
        
        var (_, _, account) = service.CreateAccount("testuser_" + Guid.NewGuid(), "password123", "Test User");
        Assert.NotNull(account);
        
        // Add initial credits
        service.AddCredits(account.Id, 10.00m);
        
        // Act - Multiple deductions
        service.DeductCredits(account.Id, 1.00m);
        service.DeductCredits(account.Id, 2.00m);
        service.DeductCredits(account.Id, 1.50m);
        
        // Assert
        var updatedAccount = service.GetAccountById(account.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal(5.50m, updatedAccount.CreditBalance); // 10.00 - 4.50
        Assert.Equal(4.50m, updatedAccount.TotalSpent); // 1.00 + 2.00 + 1.50
    }
}