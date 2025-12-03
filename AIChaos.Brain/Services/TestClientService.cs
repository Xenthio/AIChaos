using System.Text;
using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing test client connections and command routing.
/// Uses AI to automatically fix code that fails testing.
/// </summary>
public class TestClientService
{
    private readonly SettingsService _settingsService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TestClientService> _logger;
    
    // Queue for commands waiting to be tested (original prompt, current code, attempt count)
    private readonly List<TestQueueItem> _testQueue = new();
    
    // Commands that passed testing and are ready for main client
    private readonly List<(int CommandId, string Code)> _approvedQueue = new();
    
    // Track which commands are currently being tested
    private readonly Dictionary<int, PendingTest> _pendingTests = new();
    
    private readonly object _lock = new();
    
    private const int MaxFixAttempts = 3;
    
    // Use shared ground rules from AiCodeGeneratorService for error fixing prompts
    private static string TestClientAiSystemPrompt => $"""
        You are an expert Lua scripter for Garry's Mod (GLua).
        You will be given code that failed to execute on a test client, along with the error message.
        Your job is to fix the code so it runs without errors.
        
        **ERROR FIXING RULES:**
        1. Analyze the error message carefully to understand what went wrong
        2. Fix ONLY the issue causing the error - don't change working code
        3. Common GLua errors and fixes:
           - "attempt to call nil value" - the function doesn't exist, use correct function name
           - "attempt to index nil value" - checking for nil before accessing
           - "attempt to perform arithmetic on" - type conversion issues
           - "bad argument" - wrong argument type or count
           - Syntax errors - check brackets, quotes, commas
        
        4. Return ONLY the fixed raw Lua code. No markdown, no explanations.
        5. If the code references a function like RunOnClient, make sure it exists in the environment
        
        {AiCodeGeneratorService.GroundRules}
        
        **OUTPUT FORMAT:**
        Return ONLY the fixed Lua code with no markdown backticks or explanations.
        The code should be ready to execute directly.
        """;
    
    public TestClientService(SettingsService settingsService, IHttpClientFactory httpClientFactory, ILogger<TestClientService> logger)
    {
        _settingsService = settingsService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    
    /// <summary>
    /// Checks if test client mode is enabled.
    /// </summary>
    public bool IsEnabled => _settingsService.Settings.TestClient.Enabled;
    
    /// <summary>
    /// Checks if test client is currently connected (polled recently).
    /// </summary>
    public bool IsConnected
    {
        get
        {
            var lastPoll = _settingsService.Settings.TestClient.LastPollTime;
            if (!lastPoll.HasValue) return false;
            // Consider connected if polled in the last 30 seconds
            return (DateTime.UtcNow - lastPoll.Value).TotalSeconds < 30;
        }
    }
    
    /// <summary>
    /// Adds a command to the test queue. If test client mode is disabled, adds directly to approved queue.
    /// </summary>
    public void QueueForTesting(int commandId, string code, string originalPrompt = "")
    {
        lock (_lock)
        {
            if (!IsEnabled)
            {
                // If test client mode is disabled, skip testing
                _approvedQueue.Add((commandId, code));
                return;
            }
            
            var settings = _settingsService.Settings.TestClient;
            _testQueue.Add(new TestQueueItem
            {
                CommandId = commandId,
                OriginalPrompt = originalPrompt,
                CurrentCode = code,
                CleanupAfterTest = settings.CleanupAfterTest,
                AttemptCount = 0
            });
            _logger.LogInformation("[TEST CLIENT] Command #{CommandId} queued for AI-driven testing", commandId);
        }
    }
    
    /// <summary>
    /// Polls for the next command to test (called by test client).
    /// </summary>
    public TestPollResponse? PollTestCommand()
    {
        lock (_lock)
        {
            // Update last poll time
            _settingsService.UpdateTestClientConnection(true);
            
            if (_testQueue.Count == 0)
            {
                return null;
            }
            
            var item = _testQueue[0];
            _testQueue.RemoveAt(0);
            
            // Track pending test
            _pendingTests[item.CommandId] = new PendingTest
            {
                CommandId = item.CommandId,
                OriginalPrompt = item.OriginalPrompt,
                CurrentCode = item.CurrentCode,
                StartedAt = DateTime.UtcNow,
                TimeoutSeconds = _settingsService.Settings.TestClient.TimeoutSeconds,
                AttemptCount = item.AttemptCount + 1,
                CleanupAfterTest = item.CleanupAfterTest
            };
            
            _logger.LogInformation("[TEST CLIENT] Sending command #{CommandId} to test client (attempt {Attempt}/{MaxAttempts})", 
                item.CommandId, item.AttemptCount + 1, MaxFixAttempts);
            
            return new TestPollResponse
            {
                HasCode = true,
                Code = item.CurrentCode,
                CommandId = item.CommandId,
                CleanupAfterTest = item.CleanupAfterTest,
                AttemptNumber = item.AttemptCount + 1,
                MaxAttempts = MaxFixAttempts
            };
        }
    }
    
    /// <summary>
    /// Reports test result from test client. If test fails and we haven't exceeded max attempts,
    /// the AI will be asked to fix the code and it will be re-queued for testing.
    /// </summary>
    public async Task<TestResultAction> ReportTestResultAsync(int commandId, bool success, string? error)
    {
        PendingTest? pending;
        
        lock (_lock)
        {
            if (!_pendingTests.TryGetValue(commandId, out pending))
            {
                _logger.LogWarning("[TEST CLIENT] Received result for unknown command #{CommandId}", commandId);
                return TestResultAction.Unknown;
            }
            
            _pendingTests.Remove(commandId);
        }
        
        if (success)
        {
            // Test passed! Queue for main client
            lock (_lock)
            {
                _approvedQueue.Add((commandId, pending.CurrentCode));
            }
            _logger.LogInformation("[TEST CLIENT] Command #{CommandId} PASSED testing after {Attempts} attempt(s) - queued for main client", 
                commandId, pending.AttemptCount);
            return TestResultAction.Approved;
        }
        else
        {
            // Test failed - check if we can retry with AI fix
            if (pending.AttemptCount < MaxFixAttempts)
            {
                _logger.LogInformation("[TEST CLIENT] Command #{CommandId} failed (attempt {Attempt}/{MaxAttempts}), asking AI to fix...", 
                    commandId, pending.AttemptCount, MaxFixAttempts);
                
                try
                {
                    // Ask AI to fix the code
                    var fixedCode = await AskAiToFixCodeAsync(pending.CurrentCode, error ?? "Unknown error", pending.OriginalPrompt);
                    
                    if (!string.IsNullOrEmpty(fixedCode) && fixedCode != pending.CurrentCode)
                    {
                        // Re-queue with fixed code
                        lock (_lock)
                        {
                            _testQueue.Insert(0, new TestQueueItem
                            {
                                CommandId = commandId,
                                OriginalPrompt = pending.OriginalPrompt,
                                CurrentCode = fixedCode,
                                CleanupAfterTest = pending.CleanupAfterTest,
                                AttemptCount = pending.AttemptCount
                            });
                        }
                        
                        _logger.LogInformation("[TEST CLIENT] AI provided fix for command #{CommandId}, re-queued for testing", commandId);
                        return TestResultAction.Retrying;
                    }
                    else
                    {
                        _logger.LogWarning("[TEST CLIENT] AI couldn't provide a different fix for command #{CommandId}", commandId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TEST CLIENT] Failed to get AI fix for command #{CommandId}", commandId);
                }
            }
            
            // Max retries exhausted or AI couldn't fix
            _logger.LogWarning("[TEST CLIENT] Command #{CommandId} FAILED after {Attempts} attempt(s): {Error}", 
                commandId, pending.AttemptCount, error);
            return TestResultAction.Rejected;
        }
    }
    
    /// <summary>
    /// Synchronous wrapper for backward compatibility.
    /// </summary>
    public TestResultAction ReportTestResult(int commandId, bool success, string? error)
    {
        return ReportTestResultAsync(commandId, success, error).GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Asks AI to fix the code based on the error.
    /// </summary>
    private async Task<string?> AskAiToFixCodeAsync(string currentCode, string error, string originalPrompt)
    {
        var settings = _settingsService.Settings;
        
        if (string.IsNullOrEmpty(settings.OpenRouter.ApiKey))
        {
            _logger.LogWarning("[TEST CLIENT] Cannot fix code - OpenRouter API key not configured");
            return null;
        }
        
        var userContent = new StringBuilder();
        userContent.AppendLine("The following Lua code failed to execute on a Garry's Mod server.");
        userContent.AppendLine();
        userContent.AppendLine("**Original Request:**");
        userContent.AppendLine(originalPrompt);
        userContent.AppendLine();
        userContent.AppendLine("**Code that failed:**");
        userContent.AppendLine("```lua");
        userContent.AppendLine(currentCode);
        userContent.AppendLine("```");
        userContent.AppendLine();
        userContent.AppendLine("**Error message:**");
        userContent.AppendLine(error);
        userContent.AppendLine();
        userContent.AppendLine("Please fix the code so it executes without errors. Return ONLY the fixed code, no markdown or explanations.");
        
        var requestBody = new
        {
            model = settings.OpenRouter.Model,
            messages = new[]
            {
                new { role = "system", content = TestClientAiSystemPrompt },
                new { role = "user", content = userContent.ToString() }
            }
        };
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.OpenRouter.BaseUrl}/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json")
        };
        
        request.Headers.Add("Authorization", $"Bearer {settings.OpenRouter.ApiKey}");
        
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("[TEST CLIENT] AI API request failed with status {StatusCode}: {Content}", 
                response.StatusCode, errorContent);
            return null;
        }
        
        var responseContent = await response.Content.ReadAsStringAsync();
        
        try
        {
            var jsonDoc = JsonDocument.Parse(responseContent);
            
            // Validate response structure
            if (!jsonDoc.RootElement.TryGetProperty("choices", out var choices) || 
                choices.GetArrayLength() == 0)
            {
                _logger.LogWarning("[TEST CLIENT] AI response missing 'choices' array");
                return null;
            }
            
            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content))
            {
                _logger.LogWarning("[TEST CLIENT] AI response missing 'message.content'");
                return null;
            }
            
            var fixedCode = content.GetString() ?? "";
            
            // Clean up the response - remove any markdown formatting
            fixedCode = fixedCode.Trim();
            if (fixedCode.StartsWith("```lua"))
            {
                fixedCode = fixedCode[6..];
            }
            else if (fixedCode.StartsWith("```"))
            {
                fixedCode = fixedCode[3..];
            }
            if (fixedCode.EndsWith("```"))
            {
                fixedCode = fixedCode[..^3];
            }
            fixedCode = fixedCode.Trim();
            
            return fixedCode;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[TEST CLIENT] Failed to parse AI response as JSON");
            return null;
        }
    }
    
    /// <summary>
    /// Polls for the next approved command (called by main client when test mode is enabled).
    /// </summary>
    public (int CommandId, string Code)? PollApprovedCommand()
    {
        lock (_lock)
        {
            if (_approvedQueue.Count == 0)
            {
                return null;
            }
            
            var result = _approvedQueue[0];
            _approvedQueue.RemoveAt(0);
            return result;
        }
    }
    
    /// <summary>
    /// Checks for timed out tests and marks them as failed.
    /// </summary>
    public void CheckTimeouts()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var timedOut = _pendingTests
                .Where(kvp => (now - kvp.Value.StartedAt).TotalSeconds > kvp.Value.TimeoutSeconds)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var commandId in timedOut)
            {
                _pendingTests.Remove(commandId);
                _logger.LogWarning("[TEST CLIENT] Command #{CommandId} timed out - not sending to main client", commandId);
            }
        }
    }
    
    /// <summary>
    /// Gets the current test queue status.
    /// </summary>
    public TestQueueStatus GetQueueStatus()
    {
        lock (_lock)
        {
            return new TestQueueStatus
            {
                QueuedCount = _testQueue.Count,
                PendingCount = _pendingTests.Count,
                ApprovedCount = _approvedQueue.Count,
                IsConnected = IsConnected
            };
        }
    }
    
    /// <summary>
    /// Simulates a Super Chat for testing purposes.
    /// Adds credits directly to a user account by username.
    /// </summary>
    public ServiceResult SimulateSuperChat(string username, string displayName, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return ServiceResult.Fail("Username is required");
        }
        
        if (amount <= 0)
        {
            return ServiceResult.Fail("Amount must be positive");
        }
        
        _logger.LogInformation("[TESTING] Simulating Super Chat: ${Amount} for user {Username}", amount, username);
        
        // This will be handled by AccountService
        return ServiceResult.Ok($"Simulated ${amount:F2} Super Chat for {username}");
    }
    
    /// <summary>
    /// Simulates a YouTube API Super Chat callback for testing channel linking and pending credits.
    /// </summary>
    public ServiceResult SimulateYouTubeApi(string channelId, string displayName, string? message, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return ServiceResult.Fail("YouTube Channel ID is required");
        }
        
        if (amount <= 0)
        {
            return ServiceResult.Fail("Amount must be positive");
        }
        
        _logger.LogInformation("[TESTING] Simulating YouTube API Super Chat: ${Amount} from channel {ChannelId} ({DisplayName})", 
            amount, channelId, displayName);
        
        // This will be handled by AccountService via AddCreditsToChannel
        return ServiceResult.Ok($"Simulated YouTube API Super Chat: ${amount:F2} from {displayName} ({channelId})");
    }
    
    private class PendingTest
    {
        public int CommandId { get; set; }
        public string OriginalPrompt { get; set; } = "";
        public string CurrentCode { get; set; } = "";
        public DateTime StartedAt { get; set; }
        public int TimeoutSeconds { get; set; }
        public int AttemptCount { get; set; }
        public bool CleanupAfterTest { get; set; }
    }
    
    private class TestQueueItem
    {
        public int CommandId { get; set; }
        public string OriginalPrompt { get; set; } = "";
        public string CurrentCode { get; set; } = "";
        public bool CleanupAfterTest { get; set; }
        public int AttemptCount { get; set; }
    }
}

/// <summary>
/// Response for test client poll.
/// </summary>
public class TestPollResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("has_code")]
    public bool HasCode { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public string? Code { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("command_id")]
    public int? CommandId { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("cleanup_after_test")]
    public bool CleanupAfterTest { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("attempt_number")]
    public int AttemptNumber { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("max_attempts")]
    public int MaxAttempts { get; set; }
}

/// <summary>
/// Status of the test queue.
/// </summary>
public class TestQueueStatus
{
    public int QueuedCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public bool IsConnected { get; set; }
}

/// <summary>
/// What action was taken after a test result.
/// </summary>
public enum TestResultAction
{
    Unknown,
    Approved,
    Rejected,
    Retrying
}
