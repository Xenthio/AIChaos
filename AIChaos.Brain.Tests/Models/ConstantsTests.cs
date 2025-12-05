using AIChaos.Brain.Models;

namespace AIChaos.Brain.Tests.Models;

public class ConstantsTests
{
    [Fact]
    public void Constants_CommandCost_IsOneUSD()
    {
        // Assert
        Assert.Equal(1.00m, Constants.CommandCost);
    }

    [Fact]
    public void Constants_CommandCost_IsDecimal()
    {
        // Assert
        Assert.IsType<decimal>(Constants.CommandCost);
    }
}

public class PendingChannelCreditsTests
{
    [Fact]
    public void PendingChannelCredits_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var credits = new PendingChannelCredits();

        // Assert
        Assert.Equal(string.Empty, credits.ChannelId);
        Assert.Equal(string.Empty, credits.DisplayName);
        Assert.Equal(0m, credits.PendingBalance);
        Assert.NotNull(credits.Donations);
        Assert.Empty(credits.Donations);
    }

    [Fact]
    public void PendingChannelCredits_SetProperties_WorksCorrectly()
    {
        // Arrange
        var credits = new PendingChannelCredits();
        var donation = new DonationRecord
        {
            Amount = 5.00m,
            Source = "superchat",
            Message = "Test donation"
        };

        // Act
        credits.ChannelId = "channel123";
        credits.DisplayName = "Test Channel";
        credits.PendingBalance = 10.50m;
        credits.Donations.Add(donation);

        // Assert
        Assert.Equal("channel123", credits.ChannelId);
        Assert.Equal("Test Channel", credits.DisplayName);
        Assert.Equal(10.50m, credits.PendingBalance);
        Assert.Single(credits.Donations);
        Assert.Equal(5.00m, credits.Donations[0].Amount);
    }

    [Fact]
    public void DonationRecord_DefaultConstructor_InitializesWithDefaults()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var donation = new DonationRecord();
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(donation.Timestamp >= before);
        Assert.True(donation.Timestamp <= after);
        Assert.Equal(0m, donation.Amount);
        Assert.Equal("superchat", donation.Source);
        Assert.Null(donation.Message);
    }

    [Fact]
    public void DonationRecord_SetProperties_WorksCorrectly()
    {
        // Arrange
        var donation = new DonationRecord();
        var timestamp = DateTime.UtcNow;

        // Act
        donation.Timestamp = timestamp;
        donation.Amount = 2.50m;
        donation.Source = "membership";
        donation.Message = "Thanks for the stream!";

        // Assert
        Assert.Equal(timestamp, donation.Timestamp);
        Assert.Equal(2.50m, donation.Amount);
        Assert.Equal("membership", donation.Source);
        Assert.Equal("Thanks for the stream!", donation.Message);
    }

    [Fact]
    public void PendingChannelCredits_MultipleDonations_CanBeAdded()
    {
        // Arrange
        var credits = new PendingChannelCredits();
        
        // Act
        credits.Donations.Add(new DonationRecord { Amount = 1.00m });
        credits.Donations.Add(new DonationRecord { Amount = 2.00m });
        credits.Donations.Add(new DonationRecord { Amount = 3.00m });

        // Assert
        Assert.Equal(3, credits.Donations.Count);
        Assert.Equal(1.00m, credits.Donations[0].Amount);
        Assert.Equal(2.00m, credits.Donations[1].Amount);
        Assert.Equal(3.00m, credits.Donations[2].Amount);
    }
}
