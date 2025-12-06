using AIChaos.Brain.Models;

namespace AIChaos.Brain.Tests.Models;

public class AppSettingsTests
{
    [Fact]
    public void GeneralSettings_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var settings = new GeneralSettings();

        // Assert
        Assert.False(settings.StreamMode);
        Assert.False(settings.AllowWorkshopDownload);
    }

    [Fact]
    public void GeneralSettings_SetWorkshopDownload_WorksCorrectly()
    {
        // Arrange
        var settings = new GeneralSettings();

        // Act
        settings.AllowWorkshopDownload = true;

        // Assert
        Assert.True(settings.AllowWorkshopDownload);
    }

    [Fact]
    public void AppSettings_DefaultConstructor_InitializesGeneralSettings()
    {
        // Arrange & Act
        var appSettings = new AppSettings();

        // Assert
        Assert.NotNull(appSettings.General);
        Assert.False(appSettings.General.StreamMode);
        Assert.False(appSettings.General.AllowWorkshopDownload);
    }

    [Fact]
    public void GeneralSettings_BothSettings_CanBeSetIndependently()
    {
        // Arrange
        var settings = new GeneralSettings();

        // Act - Set only StreamMode
        settings.StreamMode = true;

        // Assert
        Assert.True(settings.StreamMode);
        Assert.False(settings.AllowWorkshopDownload);

        // Act - Set only AllowWorkshopDownload
        settings.StreamMode = false;
        settings.AllowWorkshopDownload = true;

        // Assert
        Assert.False(settings.StreamMode);
        Assert.True(settings.AllowWorkshopDownload);
    }
}
