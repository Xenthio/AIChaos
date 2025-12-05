using AIChaos.Brain.Models;

namespace AIChaos.Brain.Tests.Models;

public class ServiceResultTests
{
    [Fact]
    public void ServiceResult_Ok_CreatesSuccessResult()
    {
        // Arrange & Act
        var result = ServiceResult.Ok("Success message");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success message", result.Message);
        Assert.Null(result.Url);
    }

    [Fact]
    public void ServiceResult_Ok_WithUrl_CreatesSuccessResultWithUrl()
    {
        // Arrange & Act
        var result = ServiceResult.Ok("Success", "https://example.com");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success", result.Message);
        Assert.Equal("https://example.com", result.Url);
    }

    [Fact]
    public void ServiceResult_Fail_CreatesFailureResult()
    {
        // Arrange & Act
        var result = ServiceResult.Fail("Error message");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Error message", result.Message);
        Assert.Null(result.Url);
    }

    [Fact]
    public void ServiceResult_Constructor_InitializesProperties()
    {
        // Arrange & Act
        var result = new ServiceResult(true, "Test message", "https://test.com");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Test message", result.Message);
        Assert.Equal("https://test.com", result.Url);
    }

    [Fact]
    public void ServiceResultGeneric_Ok_CreatesSuccessResultWithData()
    {
        // Arrange & Act
        var result = ServiceResult<string>.Ok("test data", "Success");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("test data", result.Data);
        Assert.Equal("Success", result.Message);
    }

    [Fact]
    public void ServiceResultGeneric_Fail_CreatesFailureResultWithDefaultData()
    {
        // Arrange & Act
        var result = ServiceResult<string>.Fail("Error");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("Error", result.Message);
    }

    [Fact]
    public void ServiceResultGeneric_Constructor_InitializesProperties()
    {
        // Arrange & Act
        var result = new ServiceResult<int>(true, 42, "Success");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Data);
        Assert.Equal("Success", result.Message);
    }

    [Fact]
    public void ServiceResultGeneric_WithComplexType_Works()
    {
        // Arrange
        var complexData = new { Name = "Test", Value = 123 };

        // Act
        var result = ServiceResult<object>.Ok(complexData, "Success");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Success", result.Message);
    }
}
