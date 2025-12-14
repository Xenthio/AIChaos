using System.Text;
using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// OpenRouter implementation of the LLM service interface.
/// Provides access to multiple LLM models through the OpenRouter API.
/// Can be easily replaced with other providers (OpenAI, Anthropic, etc.) by implementing ILLMService.
/// </summary>
public class OpenRouterService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<OpenRouterService> _logger;
    
    // Semaphore to limit concurrent API calls (API throttling)
    private static readonly SemaphoreSlim _apiThrottle = new SemaphoreSlim(
        Constants.ApiThrottling.MaxConcurrentRequests, 
        Constants.ApiThrottling.MaxConcurrentRequests);

    public OpenRouterService(
        HttpClient httpClient,
        ISettingsService settingsService,
        ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Sends a chat completion request to the OpenRouter API.
    /// </summary>
    /// <param name="messages">List of chat messages (system, user, assistant)</param>
    /// <param name="model">Optional model override (uses settings default if null)</param>
    /// <param name="useThrottling">Whether to apply API throttling</param>
    /// <returns>The assistant's response content</returns>
    public async Task<string?> ChatCompletionAsync(
        List<ChatMessage> messages,
        string? model = null,
        bool useThrottling = true)
    {
        if (useThrottling)
        {
            _logger.LogDebug("[OpenRouter] Waiting for API throttle slot ({Available}/{Max} available)", 
                _apiThrottle.CurrentCount, Constants.ApiThrottling.MaxConcurrentRequests);
            
            await _apiThrottle.WaitAsync();
        }

        try
        {
            _logger.LogDebug("[OpenRouter] Making chat completion request");
            
            var settings = _settingsService.Settings;
            var targetModel = model ?? settings.OpenRouter.Model;
            
            var requestBody = new
            {
                model = targetModel,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray()
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.OpenRouter.BaseUrl}/chat/completions")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Add("Authorization", $"Bearer {settings.OpenRouter.ApiKey}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseContent);

            var content = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenRouter] Chat completion failed");
            return null;
        }
        finally
        {
            if (useThrottling)
            {
                _apiThrottle.Release();
                _logger.LogDebug("[OpenRouter] Released API throttle slot ({Available}/{Max} available)", 
                    _apiThrottle.CurrentCount, Constants.ApiThrottling.MaxConcurrentRequests);
            }
        }
    }

    /// <summary>
    /// Sends a simple chat completion request with a system prompt and user message.
    /// </summary>
    public async Task<string?> SimpleCompletionAsync(
        string systemPrompt,
        string userMessage,
        string? model = null,
        bool useThrottling = true)
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userMessage }
        };

        return await ChatCompletionAsync(messages, model, useThrottling);
    }

    /// <summary>
    /// Sends a chat completion request expecting a JSON response.
    /// Cleans up markdown formatting from the response.
    /// </summary>
    public async Task<T?> JsonCompletionAsync<T>(
        List<ChatMessage> messages,
        string? model = null,
        bool useThrottling = true) where T : class
    {
        var response = await ChatCompletionAsync(messages, model, useThrottling);
        
        if (string.IsNullOrEmpty(response))
            return null;

        try
        {
            // Clean up markdown formatting
            var jsonContent = CleanJsonResponse(response);
            
            return JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[OpenRouter] Failed to parse JSON response: {Response}", 
                response.Length > 200 ? response[..200] + "..." : response);
            return null;
        }
    }

    /// <summary>
    /// Cleans JSON from markdown code blocks and extracts the JSON object.
    /// </summary>
    private static string CleanJsonResponse(string response)
    {
        var content = response.Trim();
        
        // Extract from markdown code blocks
        if (content.Contains("```json"))
        {
            var start = content.IndexOf("```json") + 7;
            var end = content.IndexOf("```", start);
            if (end > start)
            {
                content = content[start..end].Trim();
            }
        }
        else if (content.Contains("```"))
        {
            var start = content.IndexOf("```") + 3;
            var end = content.IndexOf("```", start);
            if (end > start)
            {
                content = content[start..end].Trim();
            }
        }
        
        // Try to find JSON object in the content
        if (!content.TrimStart().StartsWith("{"))
        {
            var jsonStart = content.IndexOf('{');
            if (jsonStart >= 0)
            {
                content = content[jsonStart..];
                var jsonEnd = content.LastIndexOf('}');
                if (jsonEnd >= 0)
                {
                    content = content[..(jsonEnd + 1)];
                }
            }
        }
        
        return content;
    }

    /// <summary>
    /// Cleans Lua code from markdown formatting.
    /// </summary>
    public static string CleanLuaCode(string code)
    {
        var cleaned = code.Trim();
        
        // Remove ```lua``` blocks
        if (cleaned.StartsWith("```lua"))
            cleaned = cleaned[6..];
        else if (cleaned.StartsWith("```"))
            cleaned = cleaned[3..];
        
        if (cleaned.EndsWith("```"))
            cleaned = cleaned[..^3];
        
        return cleaned.Trim();
    }

    /// <summary>
    /// Gets the current API throttle status.
    /// </summary>
    public (int Available, int Max) GetThrottleStatus()
    {
        return (_apiThrottle.CurrentCount, Constants.ApiThrottling.MaxConcurrentRequests);
    }

    /// <summary>
    /// Checks if the OpenRouter API is configured.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(_settingsService.Settings.OpenRouter.ApiKey);
}
