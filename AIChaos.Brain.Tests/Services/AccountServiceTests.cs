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
}
