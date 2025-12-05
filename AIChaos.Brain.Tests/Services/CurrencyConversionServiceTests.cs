using AIChaos.Brain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace AIChaos.Brain.Tests.Services;

public class CurrencyConversionServiceTests
{
    private readonly Mock<ILogger<CurrencyConversionService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;

    public CurrencyConversionServiceTests()
    {
        _loggerMock = new Mock<ILogger<CurrencyConversionService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
    }

    [Fact]
    public async Task ConvertToUsdAsync_WithUSD_ReturnsSameAmount()
    {
        // Arrange
        var service = new CurrencyConversionService(_httpClient, _loggerMock.Object);
        decimal amount = 100m;

        // Act
        var result = await service.ConvertToUsdAsync(amount, "USD");

        // Assert
        Assert.Equal(amount, result);
    }

    [Fact]
    public async Task ConvertToUsdAsync_WithNullCurrencyCode_ReturnsSameAmount()
    {
        // Arrange
        var service = new CurrencyConversionService(_httpClient, _loggerMock.Object);
        decimal amount = 100m;
        string? nullCurrencyCode = null;

        // Act
        var result = await service.ConvertToUsdAsync(amount, nullCurrencyCode!);

        // Assert
        // When currency code is null, the service treats it as USD and returns the same amount
        Assert.Equal(amount, result);
    }

    [Fact]
    public async Task ConvertToUsdAsync_WithEmptyCurrencyCode_ReturnsSameAmount()
    {
        // Arrange
        var service = new CurrencyConversionService(_httpClient, _loggerMock.Object);
        decimal amount = 100m;

        // Act
        var result = await service.ConvertToUsdAsync(amount, string.Empty);

        // Assert
        Assert.Equal(amount, result);
    }

    [Fact]
    public async Task ConvertToUsdAsync_CaseInsensitive_Works()
    {
        // Arrange
        var service = new CurrencyConversionService(_httpClient, _loggerMock.Object);
        decimal amount = 100m;

        // Act
        var result1 = await service.ConvertToUsdAsync(amount, "usd");
        var result2 = await service.ConvertToUsdAsync(amount, "USD");
        var result3 = await service.ConvertToUsdAsync(amount, "Usd");

        // Assert
        Assert.Equal(amount, result1);
        Assert.Equal(amount, result2);
        Assert.Equal(amount, result3);
    }

    [Fact]
    public void GetConversionDescription_WithUSD_ReturnsSimpleFormat()
    {
        // Arrange
        var service = new CurrencyConversionService(_httpClient, _loggerMock.Object);

        // Act
        var description = service.GetConversionDescription(100m, "USD", 100m);

        // Assert
        Assert.Equal("$100.00", description);
    }

    [Fact]
    public void GetConversionDescription_WithOtherCurrency_ReturnsConversionFormat()
    {
        // Arrange
        var service = new CurrencyConversionService(_httpClient, _loggerMock.Object);

        // Act
        var description = service.GetConversionDescription(100m, "EUR", 92m);

        // Assert
        Assert.Equal("100.00 EUR → $92.00 USD", description);
    }

    [Fact]
    public void GetConversionDescription_WithDecimalValues_FormatsCorrectly()
    {
        // Arrange
        var service = new CurrencyConversionService(_httpClient, _loggerMock.Object);

        // Act
        var description = service.GetConversionDescription(99.99m, "JPY", 0.67m);

        // Assert
        Assert.Equal("99.99 JPY → $0.67 USD", description);
    }

    [Theory]
    [InlineData(0, "USD", 0)]
    [InlineData(100, "USD", 100)]
    [InlineData(1.5, "USD", 1.5)]
    [InlineData(-10, "USD", -10)]
    public async Task ConvertToUsdAsync_VariousAmounts_HandledCorrectly(decimal amount, string currency, decimal expected)
    {
        // Arrange
        var service = new CurrencyConversionService(_httpClient, _loggerMock.Object);

        // Act
        var result = await service.ConvertToUsdAsync(amount, currency);

        // Assert
        Assert.Equal(expected, result);
    }
}
