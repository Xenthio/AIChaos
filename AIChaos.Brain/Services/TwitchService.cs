using System.Text.RegularExpressions;
using AIChaos.Brain.Models;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for Twitch chat integration with OAuth support.
/// </summary>
public partial class TwitchService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly CommandQueueService _commandQueue;
    private readonly AiCodeGeneratorService _codeGenerator;
    private readonly ILogger<TwitchService> _logger;
    
    private TwitchClient? _client;
    private readonly Dictionary<string, DateTime> _cooldowns = new();
    
    public bool IsConnected => _client?.IsConnected ?? false;
    public string? ConnectedChannel { get; private set; }
    
    public TwitchService(
        SettingsService settingsService,
        CommandQueueService commandQueue,
        AiCodeGeneratorService codeGenerator,
        ILogger<TwitchService> logger)
    {
        _settingsService = settingsService;
        _commandQueue = commandQueue;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }
    
    /// <summary>
    /// Connects to Twitch chat with the stored credentials.
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        var settings = _settingsService.Settings.Twitch;
        
        if (string.IsNullOrEmpty(settings.AccessToken) || string.IsNullOrEmpty(settings.Channel))
        {
            _logger.LogWarning("Twitch not configured - missing access token or channel");
            return false;
        }
        
        try
        {
            var credentials = new ConnectionCredentials(settings.Channel, settings.AccessToken);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            
            var customClient = new WebSocketClient(clientOptions);
            _client = new TwitchClient(customClient);
            _client.Initialize(credentials, settings.Channel);
            
            _client.OnConnected += OnConnected;
            _client.OnMessageReceived += OnMessageReceived;
            _client.OnError += OnError;
            
            _client.Connect();
            
            // Wait a bit for connection
            await Task.Delay(2000);
            
            if (_client.IsConnected)
            {
                ConnectedChannel = settings.Channel;
                settings.Enabled = true;
                _settingsService.SaveSettings();
                _logger.LogInformation("Connected to Twitch channel: {Channel}", settings.Channel);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Twitch");
            return false;
        }
    }
    
    /// <summary>
    /// Disconnects from Twitch chat.
    /// </summary>
    public void Disconnect()
    {
        if (_client?.IsConnected == true)
        {
            _client.Disconnect();
        }
        ConnectedChannel = null;
        
        var settings = _settingsService.Settings.Twitch;
        settings.Enabled = false;
        _settingsService.SaveSettings();
        
        _logger.LogInformation("Disconnected from Twitch");
    }
    
    private void OnConnected(object? sender, OnConnectedArgs e)
    {
        _logger.LogInformation("Twitch client connected as {BotUsername}", e.BotUsername);
    }
    
    private async void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var settings = _settingsService.Settings.Twitch;
        var message = e.ChatMessage;
        
        // Check if message starts with the command
        if (!message.Message.StartsWith(settings.ChatCommand, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        
        // Extract the prompt
        var prompt = message.Message[settings.ChatCommand.Length..].Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            return;
        }
        
        var username = message.Username;
        
        // Get bits from the message
        var bits = message.Bits;
        
        _logger.LogInformation("[Twitch] Message from {Username}: {Message} (Bits: {Bits})", 
            username, prompt, bits);
        
        // Check if bits are required
        if (settings.RequireBits && bits < settings.MinBitsAmount)
        {
            _logger.LogInformation("[Twitch] Insufficient bits from {Username}: {Bits} < {MinBits}",
                username, bits, settings.MinBitsAmount);
            return;
        }
        
        // Check cooldown
        if (IsOnCooldown(username, settings.CooldownSeconds))
        {
            _logger.LogInformation("[Twitch] User {Username} is on cooldown", username);
            return;
        }
        
        // Filter URLs if not moderator
        var isMod = message.IsModerator || message.IsBroadcaster;
        var filteredPrompt = FilterUrls(prompt, isMod, _settingsService.Settings.Safety);
        
        // Generate and queue the code
        try
        {
            var (executionCode, undoCode, needsModeration, moderationReason) = await _codeGenerator.GenerateCodeAsync(filteredPrompt);
            
            // Check for dangerous code patterns
            if (ContainsDangerousPatterns(executionCode))
            {
                _logger.LogWarning("[Twitch] Blocked dangerous code from {Username}", username);
                return;
            }
            
            _commandQueue.AddCommand(
                filteredPrompt, 
                executionCode, 
                undoCode, 
                "twitch", 
                username);
            
            SetCooldown(username);
            _logger.LogInformation("[Twitch] Command queued from {Username}: {Prompt}", username, filteredPrompt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Twitch] Failed to process command from {Username}", username);
        }
    }
    
    private void OnError(object? sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
    {
        _logger.LogError(e.Exception, "Twitch client error");
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
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
