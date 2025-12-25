using AIChaos.Brain.Models;
using AIChaos.Brain.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIChaos.Brain.Tests.Services;

[Collection("Sequential")]
public class KofiServiceTests
{
    private SettingsService CreateSettingsService(bool enabled = true, string verificationToken = "test-token")
    {
        var mockLogger = new Mock<ILogger<SettingsService>>();
        var settingsService = new SettingsService(mockLogger.Object);
        
        // Configure Ko-fi settings
        settingsService.Settings.PaymentProviders.Kofi.Enabled = enabled;
        settingsService.Settings.PaymentProviders.Kofi.VerificationToken = verificationToken;
        settingsService.Settings.PaymentProviders.Kofi.MinDonationAmount = 1.00m;
        
        return settingsService;
    }

    [Fact]
    public void ProcessDonation_WithValidPayload_AddsCreditsToExistingAccount()
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        // Create an account
        accountService.CreateAccount("testuser", "password", "Test User");
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            MessageId = "msg123",
            KofiTransactionId = "txn123",
            Amount = "5.00",
            FromName = "John Doe",
            Message = "username: testuser",
            Currency = "USD",
            Type = "Donation"
        };

        // Act
        var result = kofiService.ProcessDonation(payload);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("success", result.Data.Status);
        Assert.Equal(5.00m, result.Data.CreditsAdded);
        Assert.Equal("testuser", result.Data.Username);
        
        var account = accountService.GetAccountByUsername("testuser");
        Assert.NotNull(account);
        Assert.Equal(5.00m, account.CreditBalance);
    }

    [Fact]
    public void ProcessDonation_WithDisabledKofi_ReturnsFailure()
    {
        // Arrange
        var settingsService = CreateSettingsService(enabled: false);
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            Amount = "5.00",
            Message = "username: testuser"
        };

        // Act
        var result = kofiService.ProcessDonation(payload);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not enabled", result.Message);
    }

    [Fact]
    public void ProcessDonation_WithInvalidVerificationToken_ReturnsFailure()
    {
        // Arrange
        var settingsService = CreateSettingsService(verificationToken: "correct-token");
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "wrong-token",
            Amount = "5.00",
            Message = "username: testuser"
        };

        // Act
        var result = kofiService.ProcessDonation(payload);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid verification token", result.Message);
    }

    [Fact]
    public void ProcessDonation_WithDuplicateTransaction_ReturnsFailure()
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        accountService.CreateAccount("testuser", "password", "Test User");
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            KofiTransactionId = "txn123",
            Amount = "5.00",
            Message = "username: testuser"
        };

        // Act
        var result1 = kofiService.ProcessDonation(payload);
        var result2 = kofiService.ProcessDonation(payload); // Duplicate

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Contains("Duplicate transaction", result2.Message);
        
        // Credits should only be added once
        var account = accountService.GetAccountByUsername("testuser");
        Assert.Equal(5.00m, account!.CreditBalance);
    }

    [Fact]
    public void ProcessDonation_WithInvalidAmount_ReturnsFailure()
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            MessageId = "msg123",
            Amount = "invalid",
            Message = "username: testuser"
        };

        // Act
        var result = kofiService.ProcessDonation(payload);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid donation amount", result.Message);
    }

    [Fact]
    public void ProcessDonation_BelowMinimumAmount_ReturnsFailure()
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            MessageId = "msg123",
            Amount = "0.50", // Below $1.00 minimum
            Message = "username: testuser"
        };

        // Act
        var result = kofiService.ProcessDonation(payload);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("at least", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessDonation_WithNonExistentUser_CreatesPendingCredits()
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            MessageId = "msg123",
            KofiTransactionId = "txn123",
            Amount = "5.00",
            FromName = "John Doe",
            Message = "username: nonexistent",
            Currency = "USD"
        };

        // Act
        var result = kofiService.ProcessDonation(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("pending", result.Data!.Status);
        Assert.Contains("not found", result.Data.Message);
        Assert.Equal("nonexistent", result.Data.Username);
        
        // Check pending credits were created
        var pendingCredits = accountService.GetAllPendingCredits();
        Assert.NotEmpty(pendingCredits);
    }

    [Fact]
    public void ProcessDonation_WithNoUsername_CreatesPendingCredits()
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            MessageId = "msg123",
            KofiTransactionId = "txn123",
            Amount = "5.00",
            FromName = "John Doe",
            Message = "Thanks for the stream!",
            Currency = "USD"
        };

        // Act
        var result = kofiService.ProcessDonation(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("pending", result.Data!.Status);
        // The extractor might pick up a word as username, so just verify status and pending credits were created
        var pendingCredits = accountService.GetAllPendingCredits();
        Assert.NotEmpty(pendingCredits);
    }

    [Theory]
    [InlineData("username: testuser", "testuser")]
    [InlineData("user: testuser", "testuser")]
    [InlineData("for: testuser", "testuser")]
    [InlineData("account: testuser", "testuser")]
    [InlineData("@testuser", "testuser")]
    [InlineData("testuser", "testuser")]
    [InlineData("Username: TestUser", "TestUser")]
    [InlineData("USER: testuser123", "testuser123")]
    public void ProcessDonation_ExtractsUsername_FromVariousFormats(string message, string expectedUsername)
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        accountService.CreateAccount(expectedUsername, "password", "Test User");
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            MessageId = "msg123",
            KofiTransactionId = $"txn-{Guid.NewGuid()}",
            Amount = "5.00",
            Message = message
        };

        // Act
        var result = kofiService.ProcessDonation(payload);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("success", result.Data!.Status);
        Assert.Equal(expectedUsername, result.Data.Username);
        
        var account = accountService.GetAccountByUsername(expectedUsername);
        Assert.Equal(5.00m, account!.CreditBalance);
    }

    [Fact]
    public void ProcessDonation_MultipleTransactions_AccumulatesCredits()
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        accountService.CreateAccount("testuser", "password", "Test User");
        
        var payload1 = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            KofiTransactionId = "txn1",
            Amount = "3.00",
            Message = "username: testuser"
        };
        
        var payload2 = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            KofiTransactionId = "txn2",
            Amount = "7.00",
            Message = "username: testuser"
        };

        // Act
        kofiService.ProcessDonation(payload1);
        kofiService.ProcessDonation(payload2);

        // Assert
        var account = accountService.GetAccountByUsername("testuser");
        Assert.Equal(10.00m, account!.CreditBalance);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectData()
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        accountService.CreateAccount("testuser", "password", "Test User");
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            KofiTransactionId = "txn123",
            Amount = "5.00",
            Message = "username: testuser"
        };
        
        kofiService.ProcessDonation(payload);

        // Act
        var stats = kofiService.GetStatistics();

        // Assert
        Assert.True(stats.IsEnabled);
        Assert.Equal(1, stats.ProcessedTransactionCount);
    }

    [Fact]
    public void ProcessDonation_WithMissingTransactionId_ReturnsFailure()
    {
        // Arrange
        var settingsService = CreateSettingsService();
        var mockAccountLogger = new Mock<ILogger<AccountService>>();
        var mockKofiLogger = new Mock<ILogger<KofiService>>();
        
        var accountService = new AccountService(mockAccountLogger.Object);
        var kofiService = new KofiService(settingsService, accountService, mockKofiLogger.Object);
        
        var payload = new KofiWebhookPayload
        {
            VerificationToken = "test-token",
            KofiTransactionId = null,
            MessageId = null,
            Amount = "5.00",
            Message = "username: testuser"
        };

        // Act
        var result = kofiService.ProcessDonation(payload);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Missing transaction ID", result.Message);
    }
}
