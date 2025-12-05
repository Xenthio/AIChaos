using AIChaos.Brain.Models;

namespace AIChaos.Brain.Tests.Models;

public class AccountTests
{
    [Fact]
    public void Account_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var account = new Account();

        // Assert
        Assert.NotNull(account.Id);
        Assert.NotEmpty(account.Id);
        Assert.Equal(string.Empty, account.Username);
        Assert.Equal(string.Empty, account.PasswordHash);
        Assert.Equal(string.Empty, account.DisplayName);
        Assert.Equal(0, account.CreditBalance);
        Assert.Equal(0, account.TotalSpent);
        Assert.Equal(DateTime.MinValue, account.LastRequestTime);
        Assert.Null(account.LinkedYouTubeChannelId);
        Assert.Null(account.PictureUrl);
        Assert.Null(account.PendingVerificationCode);
        Assert.Null(account.VerificationCodeExpiresAt);
        Assert.Null(account.SessionToken);
        Assert.Null(account.SessionExpiresAt);
        Assert.Equal(UserRole.User, account.Role);
    }

    [Fact]
    public void Account_Id_IsUniqueGuid()
    {
        // Arrange & Act
        var account1 = new Account();
        var account2 = new Account();

        // Assert
        Assert.NotEqual(account1.Id, account2.Id);
        Assert.True(Guid.TryParse(account1.Id, out _));
        Assert.True(Guid.TryParse(account2.Id, out _));
    }

    [Fact]
    public void Account_SetProperties_WorksCorrectly()
    {
        // Arrange
        var account = new Account();
        var now = DateTime.UtcNow;

        // Act
        account.Username = "testuser";
        account.PasswordHash = "hashedpassword";
        account.DisplayName = "Test User";
        account.CreditBalance = 10.5m;
        account.TotalSpent = 5.25m;
        account.LastRequestTime = now;
        account.LinkedYouTubeChannelId = "channel123";
        account.PictureUrl = "https://example.com/pic.jpg";
        account.PendingVerificationCode = "CODE123";
        account.VerificationCodeExpiresAt = now.AddMinutes(5);
        account.SessionToken = "token123";
        account.SessionExpiresAt = now.AddDays(7);
        account.Role = UserRole.Admin;

        // Assert
        Assert.Equal("testuser", account.Username);
        Assert.Equal("hashedpassword", account.PasswordHash);
        Assert.Equal("Test User", account.DisplayName);
        Assert.Equal(10.5m, account.CreditBalance);
        Assert.Equal(5.25m, account.TotalSpent);
        Assert.Equal(now, account.LastRequestTime);
        Assert.Equal("channel123", account.LinkedYouTubeChannelId);
        Assert.Equal("https://example.com/pic.jpg", account.PictureUrl);
        Assert.Equal("CODE123", account.PendingVerificationCode);
        Assert.Equal(now.AddMinutes(5), account.VerificationCodeExpiresAt);
        Assert.Equal("token123", account.SessionToken);
        Assert.Equal(now.AddDays(7), account.SessionExpiresAt);
        Assert.Equal(UserRole.Admin, account.Role);
    }

    [Fact]
    public void Account_CreatedAt_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var account = new Account();
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(account.CreatedAt >= before);
        Assert.True(account.CreatedAt <= after);
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Admin)]
    public void Account_Role_CanBeSetToAllValues(UserRole role)
    {
        // Arrange
        var account = new Account();

        // Act
        account.Role = role;

        // Assert
        Assert.Equal(role, account.Role);
    }

    [Fact]
    public void UserRole_Enum_HasCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)UserRole.User);
        Assert.Equal(1, (int)UserRole.Moderator);
        Assert.Equal(2, (int)UserRole.Admin);
    }
}
