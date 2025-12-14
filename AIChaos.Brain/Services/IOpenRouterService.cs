using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Interface for interacting with the OpenRouter API (or other compatible LLM APIs).
/// Provides a clean abstraction for chat completions with support for:
/// - Multiple message formats (system, user, assistant)
/// - Structured JSON responses
/// - API throttling
/// - Multiple model support
/// </summary>
public interface IOpenRouterService
{
    /// <summary>
    /// Sends a chat completion request to the OpenRouter API.
    /// </summary>
    /// <param name="messages">List of chat messages (system, user, assistant)</param>
    /// <param name="model">Optional model override (uses settings default if null)</param>
    /// <param name="useThrottling">Whether to apply API throttling</param>
    /// <returns>The assistant's response content</returns>
    Task<string?> ChatCompletionAsync(
        List<ChatMessage> messages,
        string? model = null,
        bool useThrottling = true);

    /// <summary>
    /// Sends a simple chat completion request with a system prompt and user message.
    /// </summary>
    Task<string?> SimpleCompletionAsync(
        string systemPrompt,
        string userMessage,
        string? model = null,
        bool useThrottling = true);

    /// <summary>
    /// Sends a chat completion request expecting a JSON response.
    /// Cleans up markdown formatting from the response.
    /// </summary>
    Task<T?> JsonCompletionAsync<T>(
        List<ChatMessage> messages,
        string? model = null,
        bool useThrottling = true) where T : class;

    /// <summary>
    /// Gets the current API throttle status.
    /// </summary>
    (int Available, int Max) GetThrottleStatus();

    /// <summary>
    /// Checks if the OpenRouter API is configured.
    /// </summary>
    bool IsConfigured { get; }
}
