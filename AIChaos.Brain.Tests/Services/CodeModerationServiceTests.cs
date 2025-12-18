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
}
