using System.Text.RegularExpressions;
using System.Text.Json;
using AIChaos.Brain.Models;
using AIChaos.Brain.Helpers;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for YouTube Live Chat integration with OAuth support.
/// </summary>
public partial class YouTubeService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly CommandQueueService _commandQueue;
    private readonly AiCodeGeneratorService _codeGenerator;
    private readonly AccountService _accountService;
    private readonly CurrencyConversionService _currencyConverter;
    private readonly ILogger<YouTubeService> _logger;
    private readonly HttpClient _httpClient;

    private Google.Apis.YouTube.v3.YouTubeService? _youtubeService;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _pollingTask;
    private readonly Dictionary<string, DateTime> _cooldowns = new();
    private readonly SemaphoreSlim _listenerLock = new(1, 1);
    private bool _isPolling = false;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private const int TokenRefreshBufferMinutes = 5; // Refresh token 5 minutes before expiry

    public bool IsListening => _isPolling && _pollingTask != null && !_pollingTask.IsCompleted;
    public string? CurrentVideoId { get; private set; }
    public string? LiveChatId { get; private set; }

    public YouTubeService(
        SettingsService settingsService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        AccountService accountService,
        CurrencyConversionService currencyConverter,
        ILogger<YouTubeService> logger)
    {
        _settingsService = settingsService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _accountService = accountService;
        _currencyConverter = currencyConverter;
        _logger = logger;
        _httpClient = new HttpClient();
    }
    
    /// <summary>
    /// Gets the YouTube OAuth authorization URL.
    /// </summary>
    public string? GetAuthorizationUrl(string redirectUri)
    {
        var settings = _settingsService.Settings.YouTube;
        
        if (string.IsNullOrEmpty(settings.ClientId))
        {
            _logger.LogWarning("YouTube Client ID not configured");
            return null;
        }
        
        var scopes = "https://www.googleapis.com/auth/youtube.readonly";
        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={settings.ClientId}" +
            $"&redirect_uri={System.Web.HttpUtility.UrlEncode(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={System.Web.HttpUtility.UrlEncode(scopes)}" +
            $"&access_type=offline" +
            $"&prompt=consent";
        
        return authUrl;
    }

    /// <summary>
    /// Refreshes the access token using the stored refresh token.
    /// </summary>
    public async Task<bool> RefreshAccessTokenAsync()
    {
        var settings = _settingsService.Settings.YouTube;
        
        if (string.IsNullOrEmpty(settings.RefreshToken))
        {
            _logger.LogWarning("[YouTube] Cannot refresh token - no refresh token stored. User needs to re-authorize.");
            return false;
        }
        
        if (string.IsNullOrEmpty(settings.ClientId) || string.IsNullOrEmpty(settings.ClientSecret))
        {
            _logger.LogWarning("[YouTube] Cannot refresh token - missing client credentials.");
            return false;
        }
        
        try
        {
            _logger.LogInformation("[YouTube] Refreshing access token...");
            
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["refresh_token"] = settings.RefreshToken,
                ["grant_type"] = "refresh_token"
            });
            
            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[YouTube] Token refresh failed: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                    
                // If refresh token is invalid, clear it so user knows to re-authorize
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    _logger.LogWarning("[YouTube] Refresh token appears to be invalid. User needs to re-authorize YouTube.");
                }
                return false;
            }
            
            var tokenJson = await response.Content.ReadAsStringAsync();
            var tokenData = JsonDocument.Parse(tokenJson);
            
            var newAccessToken = tokenData.RootElement.GetProperty("access_token").GetString();
            var expiresIn = tokenData.RootElement.TryGetProperty("expires_in", out var expiresInProp) 
                ? expiresInProp.GetInt32() 
                : 3600; // Default 1 hour
            
            if (string.IsNullOrEmpty(newAccessToken))
            {
                _logger.LogError("[YouTube] Token refresh returned empty access token");
                return false;
            }
            
            // Update settings with new access token
            settings.AccessToken = newAccessToken;
            _settingsService.UpdateYouTube(settings);
            
            // Track when this token expires
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            
            _logger.LogInformation("[YouTube] Access token refreshed successfully. Expires in {ExpiresIn} seconds", expiresIn);
            
            // Reinitialize the YouTube service with the new token
            return await InitializeServiceWithTokenAsync(newAccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YouTube] Failed to refresh access token");
            return false;
        }
    }
    
    /// <summary>
    /// Checks if the token needs to be refreshed and refreshes it if necessary.
    /// </summary>
    public async Task<bool> EnsureValidTokenAsync()
    {
        // If we haven't set an expiry time, assume we need to check on first API call failure
        if (_tokenExpiresAt == DateTime.MinValue)
        {
            return true; // Let it try with current token
        }
        
        // Check if token is expiring soon (within buffer time)
        var expiresIn = _tokenExpiresAt - DateTime.UtcNow;
        if (expiresIn.TotalMinutes <= TokenRefreshBufferMinutes)
        {
            _logger.LogInformation("[YouTube] Token expiring in {Minutes:F1} minutes, refreshing...", expiresIn.TotalMinutes);
            return await RefreshAccessTokenAsync();
        }
        
        return true;
    }
    
    /// <summary>
    /// Initializes the YouTube service with a specific access token.
    /// </summary>
    private async Task<bool> InitializeServiceWithTokenAsync(string accessToken)
    {
        try
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);

            _youtubeService = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "AIChaos Brain"
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YouTube] Failed to initialize service with new token");
            return false;
        }
    }

    /// <summary>
    /// Initializes the YouTube service with stored credentials.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        var settings = _settingsService.Settings.YouTube;

        if (string.IsNullOrEmpty(settings.AccessToken))
        {
            _logger.LogWarning("YouTube not configured - missing access token");
            return false;
        }

        try
        {
            var credential = GoogleCredential.FromAccessToken(settings.AccessToken);

            _youtubeService = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "AIChaos Brain"
            });
            
            // Set initial token expiry - assume 1 hour from now if we don't know
            // This will be corrected after first token refresh
            if (_tokenExpiresAt == DateTime.MinValue)
            {
                _tokenExpiresAt = DateTime.UtcNow.AddMinutes(55); // Assume ~55 mins remaining
            }

            _logger.LogInformation("YouTube service initialized");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize YouTube service");
            return false;
        }
    }

    /// <summary>
    /// Gets the live chat ID for a video.
    /// </summary>
    public async Task<string?> GetLiveChatIdAsync(string videoId)
    {
        if (_youtubeService == null)
        {
            return null;
        }

        try
        {
            var request = _youtubeService.Videos.List("liveStreamingDetails");
            request.Id = videoId;

            var response = await request.ExecuteAsync();
            var video = response.Items?.FirstOrDefault();

            return video?.LiveStreamingDetails?.ActiveLiveChatId;
        }
        catch (Google.GoogleApiException apiEx) when (apiEx.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogError("[YouTube] QUOTA EXCEEDED! Cannot get live chat ID for video {VideoId}", videoId);
            _logger.LogError("[YouTube] Your YouTube API quota has been exhausted. It resets daily (usually midnight Pacific Time).");
            _logger.LogError("[YouTube] To reduce quota usage:");
            _logger.LogError("[YouTube]   1. Increase polling interval (currently checking every 10+ seconds)");
            _logger.LogError("[YouTube]   2. Request a quota increase at: https://console.cloud.google.com/apis/api/youtube.googleapis.com/quotas");
            _logger.LogError("[YouTube]   3. Consider using YouTube's EventSub webhooks instead of polling (requires server setup)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get live chat ID for video {VideoId}", videoId);
            return null;
        }
    }

    /// <summary>
    /// Starts listening to YouTube live chat.
    /// </summary>
    public async Task<bool> StartListeningAsync(string? videoId = null)
    {
        // Try to acquire the lock - if we can't, another listener is already starting/running
        if (!await _listenerLock.WaitAsync(0))
        {
            _logger.LogWarning("[YouTube] Cannot start listening - a listener is already running or being started");
            return false;
        }

        try
        {
            // Double-check we're not already polling
            if (_isPolling)
            {
                _logger.LogWarning("[YouTube] Cannot start listening - already polling");
                return false;
            }

            var settings = _settingsService.Settings.YouTube;
            videoId ??= settings.VideoId;

            if (string.IsNullOrEmpty(videoId))
            {
                _logger.LogWarning("No video ID provided");
                return false;
            }

            if (!await InitializeAsync())
            {
                return false;
            }

            var liveChatId = await GetLiveChatIdAsync(videoId);
            if (string.IsNullOrEmpty(liveChatId))
            {
                _logger.LogWarning("Could not find live chat for video {VideoId}", videoId);
                return false;
            }

            // Cancel any existing polling task
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }

            CurrentVideoId = videoId;
            LiveChatId = liveChatId;
            _isPolling = true;

            _cancellationTokenSource = new CancellationTokenSource();
            _pollingTask = PollLiveChatAsync(liveChatId, _cancellationTokenSource.Token);

            settings.VideoId = videoId;
            settings.Enabled = true;
            _settingsService.SaveSettings();

            _logger.LogInformation("[YouTube] Started listening to YouTube live chat for video {VideoId}", videoId);
            return true;
        }
        finally
        {
            _listenerLock.Release();
        }
    }

    /// <summary>
    /// Stops listening to YouTube live chat.
    /// </summary>
    public void StopListening()
    {
        _logger.LogInformation("[YouTube] Stopping YouTube live chat listener...");
        
        _isPolling = false;
        _cancellationTokenSource?.Cancel();
        CurrentVideoId = null;
        LiveChatId = null;

        var settings = _settingsService.Settings.YouTube;
        settings.Enabled = false;
        _settingsService.SaveSettings();

        _logger.LogInformation("[YouTube] Stopped listening to YouTube live chat");
    }

    private async Task PollLiveChatAsync(string liveChatId, CancellationToken cancellationToken)
    {
        var settings = _settingsService.Settings.YouTube;
        string? nextPageToken = null;
        var pollCount = 0;

        _logger.LogInformation("[YouTube] Starting live chat polling loop for chat ID: {LiveChatId}", liveChatId);
        _logger.LogInformation("[YouTube] Polling interval: {Interval} seconds (configurable in settings)", settings.PollingIntervalSeconds);

        while (!cancellationToken.IsCancellationRequested && _isPolling)
        {
            try
            {
                if (_youtubeService == null)
                {
                    _logger.LogWarning("[YouTube] YouTube service is null, stopping polling");
                    break;
                }
                
                // Proactively refresh token before it expires
                if (!await EnsureValidTokenAsync())
                {
                    _logger.LogError("[YouTube] Failed to ensure valid token, stopping polling");
                    _isPolling = false;
                    break;
                }

                pollCount++;
                _logger.LogDebug("[YouTube] Poll #{PollCount} starting...", pollCount);

                var request = _youtubeService.LiveChatMessages.List(liveChatId, "snippet,authorDetails");
                request.PageToken = nextPageToken;

                var response = await request.ExecuteAsync(cancellationToken);
                nextPageToken = response.NextPageToken;

                foreach (var message in response.Items ?? [])
                {
                    _ = ProcessMessageAsync(message); // Fire-and-forget intentionally
                }

                // Wait before next poll - use the configured interval (in milliseconds)
                var configuredMinInterval = settings.PollingIntervalSeconds * 1000;
                var suggestedDelay = (int)(response.PollingIntervalMillis ?? configuredMinInterval);
                var delay = Math.Max(suggestedDelay, configuredMinInterval);
                
                // Additional safety check - if delay is somehow less than 1 second, force it to configured minimum
                if (delay < 1000)
                {
                    _logger.LogWarning("[YouTube] Detected suspiciously low polling interval ({Delay}ms), forcing to {ConfiguredMin}ms", 
                        delay, configuredMinInterval);
                    delay = configuredMinInterval;
                }
                
                _logger.LogDebug("[YouTube] Poll #{PollCount} complete. Next poll in {Delay}ms", pollCount, delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[YouTube] Polling cancelled after {PollCount} polls", pollCount);
                break;
            }
            catch (Google.GoogleApiException apiEx) when (apiEx.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError("[YouTube] Quota exceeded after {PollCount} polls! Stopping live chat polling.", pollCount);
                _logger.LogError("[YouTube] Please wait until your quota resets (usually midnight Pacific Time).");
                
                // Stop polling to prevent further quota usage
                _isPolling = false;
                break;
            }
            catch (Google.GoogleApiException apiEx) when (apiEx.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("[YouTube] Access token expired (poll #{PollCount}). Attempting to refresh...", pollCount);
                
                // Try to refresh the token
                if (await RefreshAccessTokenAsync())
                {
                    _logger.LogInformation("[YouTube] Token refreshed successfully, continuing polling");
                    // Continue the loop - the service was reinitialized with new token
                    continue;
                }
                else
                {
                    _logger.LogError("[YouTube] Failed to refresh token. Stopping polling. User needs to re-authorize YouTube.");
                    _isPolling = false;
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[YouTube] Error polling YouTube live chat (poll #{PollCount})", pollCount);
                // Use configured interval on error
                await Task.Delay(settings.PollingIntervalSeconds * 1000, cancellationToken);
            }
        }

        _logger.LogInformation("[YouTube] Polling loop ended after {PollCount} polls. Reason: {Reason}", 
            pollCount, 
            cancellationToken.IsCancellationRequested ? "Cancelled" : !_isPolling ? "Stopped" : "Unknown");
        
        _isPolling = false;
    }

    private async Task ProcessMessageAsync(LiveChatMessage message)
    {
        var settings = _settingsService.Settings.YouTube;
        var snippet = message.Snippet;
        var author = message.AuthorDetails;

        if (snippet == null || author == null) return;

        var messageText = snippet.DisplayMessage ?? "";
        var username = author.DisplayName ?? "Unknown";
        var channelId = author.ChannelId ?? "";

        _logger.LogDebug("[YouTube] Processing message from {Username}: {Message}", username, messageText);

        // Check ALL messages for verification codes (allows linking via regular chat)
        var (linked, accountId) = _accountService.CheckAndLinkFromChatMessage(channelId, messageText, username);
        if (linked)
        {
            _logger.LogInformation("[YouTube] ✓ Channel {ChannelId} linked to account {AccountId} via chat message!", channelId, accountId);
        }

        // Check for Super Chat
        var isSuperChat = snippet.Type == "superChatEvent" || snippet.Type == "superStickerEvent";
        decimal superChatAmountUsd = 0;
        string? currencyCode = null;
        decimal originalAmount = 0;

        if (isSuperChat && snippet.SuperChatDetails != null)
        {
            // Get the amount in the original currency (converted from micros)
            originalAmount = (decimal)(snippet.SuperChatDetails.AmountMicros ?? 0) / 1_000_000m;
            currencyCode = snippet.SuperChatDetails.Currency;
            
            // Convert to USD
            superChatAmountUsd = await _currencyConverter.ConvertToUsdAsync(originalAmount, currencyCode ?? "USD");
            
            messageText = snippet.SuperChatDetails.UserComment ?? messageText;
        }

        // Only process Super Chats for credits
        if (!isSuperChat || superChatAmountUsd < settings.MinSuperChatAmount)
        {
            return;
        }

        // Log with currency conversion details
        var conversionDesc = _currencyConverter.GetConversionDescription(originalAmount, currencyCode ?? "USD", superChatAmountUsd);
        _logger.LogInformation("[YouTube] Super Chat from {Username} ({ChannelId}): {ConversionDesc}",
            username, channelId, conversionDesc);
        
        // Add credits based on USD amount - this will go to the account if linked, or store as pending if not
        try
        {
            _accountService.AddCreditsToChannel(channelId, superChatAmountUsd, username, messageText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YouTube] Failed to add credits for {Username}", username);
        }
    }

    private bool IsOnCooldown(string username, int cooldownSeconds)
    {
        if (_cooldowns.TryGetValue(username.ToLowerInvariant(), out var lastCommand))
        {
            return (DateTime.UtcNow - lastCommand).TotalSeconds < cooldownSeconds;
        }
        return false;
    }

    private void SetCooldown(string username)
    {
        _cooldowns[username.ToLowerInvariant()] = DateTime.UtcNow;
    }

    public void Dispose()
    {
        StopListening();
        _youtubeService?.Dispose();
        _cancellationTokenSource?.Dispose();
        _listenerLock?.Dispose();
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
