using System.Text.RegularExpressions;
using AIChaos.Brain.Models;
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
    private readonly ILogger<YouTubeService> _logger;

    private Google.Apis.YouTube.v3.YouTubeService? _youtubeService;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _pollingTask;
    private readonly Dictionary<string, DateTime> _cooldowns = new();

    public bool IsListening => _pollingTask != null && !_pollingTask.IsCompleted;
    public string? CurrentVideoId { get; private set; }
    public string? LiveChatId { get; private set; }

    public YouTubeService(
        SettingsService settingsService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        AccountService accountService,
        ILogger<YouTubeService> logger)
    {
        _settingsService = settingsService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _accountService = accountService;
        _logger = logger;
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

        CurrentVideoId = videoId;
        LiveChatId = liveChatId;

        _cancellationTokenSource = new CancellationTokenSource();
        _pollingTask = PollLiveChatAsync(liveChatId, _cancellationTokenSource.Token);

        settings.VideoId = videoId;
        settings.Enabled = true;
        _settingsService.SaveSettings();

        _logger.LogInformation("Started listening to YouTube live chat for video {VideoId}", videoId);
        return true;
    }

    /// <summary>
    /// Stops listening to YouTube live chat.
    /// </summary>
    public void StopListening()
    {
        _cancellationTokenSource?.Cancel();
        CurrentVideoId = null;
        LiveChatId = null;

        var settings = _settingsService.Settings.YouTube;
        settings.Enabled = false;
        _settingsService.SaveSettings();

        _logger.LogInformation("Stopped listening to YouTube live chat");
    }

    private async Task PollLiveChatAsync(string liveChatId, CancellationToken cancellationToken)
    {
        var settings = _settingsService.Settings.YouTube;
        string? nextPageToken = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_youtubeService == null) break;

                var request = _youtubeService.LiveChatMessages.List(liveChatId, "snippet,authorDetails");
                request.PageToken = nextPageToken;

                var response = await request.ExecuteAsync(cancellationToken);
                nextPageToken = response.NextPageToken;

                foreach (var message in response.Items ?? [])
                {
                    ProcessMessageAsync(message);
                }

                // Wait before next poll (use pollingIntervalMillis from response)
                var delay = (int)(response.PollingIntervalMillis ?? 5000);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling YouTube live chat");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private void ProcessMessageAsync(LiveChatMessage message)
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
        decimal superChatAmount = 0;

        if (isSuperChat && snippet.SuperChatDetails != null)
        {
            superChatAmount = (decimal)(snippet.SuperChatDetails.AmountMicros ?? 0) / 1_000_000m;
            messageText = snippet.SuperChatDetails.UserComment ?? messageText;
        }

        // Only process Super Chats for credits
        if (!isSuperChat || superChatAmount < settings.MinSuperChatAmount)
        {
            return;
        }

        _logger.LogInformation("[YouTube] Super Chat from {Username} ({ChannelId}): ${Amount}",
            username, channelId, superChatAmount);
        
        // Add credits - this will go to the account if linked, or store as pending if not
        try
        {
            _accountService.AddCreditsToChannel(channelId, superChatAmount, username, messageText);
            // Note: Legacy UserService credits are handled separately through AddCreditsToChannel
            // when an account is not linked, to avoid double-crediting
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

    private static string FilterUrls(string message, bool isMod, SafetySettings safety)
    {
        if (!safety.BlockUrls || isMod)
        {
            return message;
        }

        var urlPattern = UrlRegex();
        var matches = urlPattern.Matches(message);
        var filtered = message;

        foreach (Match match in matches)
        {
            var url = match.Value;
            try
            {
                var uri = new Uri(url);
                var domain = uri.Host.ToLowerInvariant();

                var isAllowed = safety.AllowedDomains.Any(d =>
                    domain.Contains(d, StringComparison.OrdinalIgnoreCase));

                if (!isAllowed)
                {
                    filtered = filtered.Replace(url, "[URL REMOVED]");
                }
            }
            catch
            {
                filtered = filtered.Replace(url, "[URL REMOVED]");
            }
        }

        return filtered;
    }

    private static bool ContainsDangerousPatterns(string code)
    {
        var dangerousPatterns = new[]
        {
            "changelevel",
            @"RunConsoleCommand.*""map""",
            @"RunConsoleCommand.*'map'",
            @"game\.ConsoleCommand.*map"
        };

        return dangerousPatterns.Any(pattern =>
            Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase));
    }

    [GeneratedRegex(@"http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&+]|[!*\\(\\),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+")]
    private static partial Regex UrlRegex();

    public void Dispose()
    {
        StopListening();
        _youtubeService?.Dispose();
        _cancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
