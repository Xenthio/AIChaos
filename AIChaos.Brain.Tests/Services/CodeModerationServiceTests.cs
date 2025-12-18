using AIChaos.Brain.Services;
using Xunit;

namespace AIChaos.Brain.Tests.Services;

public class CodeModerationServiceTests
{
    [Fact]
    public void GetFilteredPatternReason_WithDownloadAndSpawn_ReturnsWorkshopSmartDownloadSpawn()
    {
        // Arrange
        var code = "DownloadAndSpawn(\"123\", function() end)";
        
        // Act
        var reason = CodeModerationService.GetFilteredPatternReason(code);
        
        // Assert
        Assert.NotNull(reason);
        Assert.Equal("Workshop smart download/spawn", reason);
    }

    [Fact]
    public void GetFilteredPatternReason_WithDownloadAndSpawnWhitespace_ReturnsWorkshopSmartDownloadSpawn()
    {
        // Arrange
        var code = "DownloadAndSpawn   (  \"456\"  , function() end)";
        
        // Act
        var reason = CodeModerationService.GetFilteredPatternReason(code);
        
        // Assert
        Assert.NotNull(reason);
        Assert.Equal("Workshop smart download/spawn", reason);
    }

    [Theory]
    [InlineData("downloadandspawn(\"789\")")]
    [InlineData("DOWNLOADANDSPAWN(\"789\")")]
    public void GetFilteredPatternReason_WithDownloadAndSpawnDifferentCasing_ReturnsWorkshopSmartDownloadSpawn(string code)
    {
        // Act
        var reason = CodeModerationService.GetFilteredPatternReason(code);
        
        // Assert
        Assert.NotNull(reason);
        Assert.Equal("Workshop smart download/spawn", reason);
    }

    [Fact]
    public void GetFilteredPatternReason_WithSimilarFunctionName_DoesNotMatch()
    {
        // Arrange
        var code = "DownloadAndSpawnCustom(\"123\")";
        
        // Act
        var reason = CodeModerationService.GetFilteredPatternReason(code);
        
        // Assert
        Assert.Null(reason);
    }
}
