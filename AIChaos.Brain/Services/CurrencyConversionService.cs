using System.Text.Json;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for converting currencies to USD using exchange rates.
/// </summary>
public class CurrencyConversionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CurrencyConversionService> _logger;
    private readonly Dictionary<string, decimal> _exchangeRates = new();
    private DateTime _lastUpdate = DateTime.MinValue;
    private const int CacheHours = 6; // Cache exchange rates for 6 hours

    public CurrencyConversionService(HttpClient httpClient, ILogger<CurrencyConversionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Converts an amount in the given currency to USD.
    /// </summary>
    /// <param name="amount">Amount in the original currency</param>
    /// <param name="currencyCode">ISO 4217 currency code (e.g., "JPY", "EUR", "GBP")</param>
    /// <returns>Amount in USD</returns>
    public async Task<decimal> ConvertToUsdAsync(decimal amount, string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode))
        {
            _logger.LogWarning("Currency code not provided, assuming USD");
            return amount;
        }

        // Already USD
        if (currencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        // Refresh exchange rates if cache is stale
        if (DateTime.UtcNow - _lastUpdate > TimeSpan.FromHours(CacheHours))
        {
            await RefreshExchangeRatesAsync();
        }

        // Try to convert using cached rates
        if (_exchangeRates.TryGetValue(currencyCode.ToUpperInvariant(), out var rateToUsd))
        {
            var usdAmount = amount * rateToUsd;
            _logger.LogInformation("Converted {Amount} {Currency} to ${UsdAmount:F2} USD (rate: {Rate})",
                amount, currencyCode, usdAmount, rateToUsd);
            return usdAmount;
        }

        _logger.LogWarning("Currency {Currency} not found in exchange rates, assuming USD", currencyCode);
        return amount; // Fallback to original amount if conversion fails
    }

    /// <summary>
    /// Refreshes exchange rates from a free API.
    /// Uses exchangerate-api.com's free tier (1,500 requests/month).
    /// </summary>
    private async Task RefreshExchangeRatesAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing currency exchange rates...");

            // Using exchangerate-api.com free tier (no API key required for basic usage)
            // Alternative: api.exchangerate-api.com/v4/latest/USD (no key needed)
            var response = await _httpClient.GetAsync("https://open.er-api.com/v6/latest/USD");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch exchange rates: {Status}", response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("rates", out var rates))
            {
                _logger.LogWarning("Exchange rates API response missing 'rates' property");
                return;
            }

            _exchangeRates.Clear();

            // Parse rates - format is { "USD": 1, "EUR": 0.92, "JPY": 149.5, etc. }
            foreach (var rate in rates.EnumerateObject())
            {
                if (rate.Value.TryGetDecimal(out var value))
                {
                    // Convert rate to "X currency = 1 USD" format
                    // API gives us "1 USD = X currency", so we need the inverse
                    _exchangeRates[rate.Name] = 1m / value;
                }
            }

            _lastUpdate = DateTime.UtcNow;
            _logger.LogInformation("Updated {Count} currency exchange rates", _exchangeRates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh exchange rates");
        }
    }

    /// <summary>
    /// Gets a human-readable description of the conversion.
    /// </summary>
    public string GetConversionDescription(decimal originalAmount, string currencyCode, decimal usdAmount)
    {
        if (currencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            return $"${usdAmount:F2}";
        }

        return $"{originalAmount:F2} {currencyCode} → ${usdAmount:F2} USD";
    }
}
