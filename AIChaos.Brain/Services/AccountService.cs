using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing user accounts, authentication, and YouTube linking.
/// </summary>
public class AccountService
{
    private readonly string _accountsPath;
    private readonly string _pendingCreditsPath;
    private readonly ILogger<AccountService> _logger;
    private readonly ConcurrentDictionary<string, Account> _accounts = new(); // by ID
    private readonly ConcurrentDictionary<string, string> _usernameIndex = new(); // username -> ID
    private readonly ConcurrentDictionary<string, string> _youtubeIndex = new(); // YouTube Channel ID -> Account ID
    private readonly ConcurrentDictionary<string, string> _sessionIndex = new(); // Session Token -> Account ID
    private readonly ConcurrentDictionary<string, PendingChannelCredits> _pendingCredits = new(); // YouTube Channel ID -> Pending Credits
    private readonly object _lock = new();

    private const int DEFAULT_RATE_LIMIT_SECONDS = 20;
    private const int VERIFICATION_CODE_EXPIRY_MINUTES = 30;
    private const int SESSION_EXPIRY_DAYS = 30;
    
    /// <summary>
    /// The ID used for the anonymous user in single-user mode.
    /// </summary>
    public const string ANONYMOUS_USER_ID = "anonymous-default-user";

    /// <summary>
    /// Event fired when a YouTube channel is successfully linked to an account.
    /// Parameters: (channelId, username)
    /// </summary>
    public event Action<string, string>? OnChannelLinked;

    public AccountService(ILogger<AccountService> logger)
    {
        _logger = logger;
        _accountsPath = Path.Combine(AppContext.BaseDirectory, "accounts.json");
        _pendingCreditsPath = Path.Combine(AppContext.BaseDirectory, "pending_credits.json");
        LoadAccounts();
        LoadPendingCredits();
        
        // Automatically unlink accounts with incorrect YouTube IDs on startup
        RunMigration();
    }
    
    /// <summary>
    /// Runs automatic migration to unlink accounts with incorrect YouTube IDs.
    /// This is called automatically on service initialization.
    /// </summary>
    private void RunMigration()
    {
        try
        {
            var accountsToUnlink = GetAccountsWithIncorrectYouTubeIds();
            
            if (accountsToUnlink.Count > 0)
            {
                _logger.LogWarning("[MIGRATION] Found {Count} accounts with incorrect YouTube IDs (Google IDs instead of Channel IDs). Starting automatic migration...", 
                    accountsToUnlink.Count);
                
                int unlinkedCount = 0;
                foreach (var account in accountsToUnlink)
                {
                    if (UnlinkYouTubeChannel(account.Id))
                    {
                        unlinkedCount++;
                        _logger.LogInformation("[MIGRATION] Unlinked incorrect YouTube ID from account {Username} ({AccountId}). User can now relink with correct channel ID.",
                            account.Username, account.Id);
                    }
                }
                
                _logger.LogWarning("[MIGRATION] Successfully unlinked {UnlinkedCount} accounts. Users can now relink their YouTube channels to get correct channel IDs and access pending donations.",
                    unlinkedCount);
            }
            else
            {
                _logger.LogInformation("[MIGRATION] No accounts with incorrect YouTube IDs found. Migration not needed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MIGRATION] Error during automatic YouTube ID migration");
        }
    }

    /// <summary>
    /// Gets or creates the default anonymous account for single-user mode.
    /// </summary>
    public Account GetOrCreateAnonymousAccount()
    {
        if (_accounts.TryGetValue(ANONYMOUS_USER_ID, out var account))
        {
            return account;
        }

        lock (_lock)
        {
            // Double-check after acquiring lock
            if (_accounts.TryGetValue(ANONYMOUS_USER_ID, out account))
            {
                return account;
            }

            // Create the anonymous account
            account = new Account
            {
                Id = ANONYMOUS_USER_ID,
                Username = "anonymous",
                DisplayName = "Anonymous User",
                CreditBalance = decimal.MaxValue, // Unlimited credits
                CreatedAt = DateTime.UtcNow,
                Role = UserRole.User
            };

            _accounts[ANONYMOUS_USER_ID] = account;
            _usernameIndex["anonymous"] = ANONYMOUS_USER_ID;
            SaveAccounts();

            _logger.LogInformation("[ACCOUNT] Created anonymous account for single-user mode");

            return account;
        }
    }

    /// <summary>
    /// Creates a new account.
    /// </summary>
    public (bool Success, string? Error, Account? Account) CreateAccount(string username, string password, string? displayName = null)
    {
        username = username.Trim().ToLowerInvariant();
        
        if (string.IsNullOrEmpty(username) || username.Length < 3)
        {
            return (false, "Username must be at least 3 characters", null);
        }
        
        if (string.IsNullOrEmpty(password) || password.Length < 4)
        {
            return (false, "Password must be at least 4 characters", null);
        }
        
        if (_usernameIndex.ContainsKey(username))
        {
            return (false, "Username already taken", null);
        }
        
        // First non-anonymous account created becomes admin
        var isFirstAccount = _accounts.Count == 0 || (_accounts.Count == 1 && _accounts.ContainsKey(ANONYMOUS_USER_ID));
        
        var account = new Account
        {
            Username = username,
            PasswordHash = HashPassword(password),
            DisplayName = displayName ?? username,
            CreatedAt = DateTime.UtcNow,
            Role = isFirstAccount ? UserRole.Admin : UserRole.User
        };
        
        _accounts[account.Id] = account;
        _usernameIndex[username] = account.Id;
        SaveAccounts();
        
        if (isFirstAccount)
        {
            _logger.LogInformation("[ACCOUNT] Created FIRST account (Admin): {Username} ({Id})", username, account.Id);
        }
        else
        {
            _logger.LogInformation("[ACCOUNT] Created account: {Username} ({Id})", username, account.Id);
        }
        
        return (true, null, account);
    }

    /// <summary>
    /// Authenticates a user and creates a session.
    /// </summary>
    public (bool Success, string? Error, Account? Account, string? SessionToken) Login(string username, string password)
    {
        username = username.Trim().ToLowerInvariant();
        
        if (!_usernameIndex.TryGetValue(username, out var accountId))
        {
            return (false, "Invalid username or password", null, null);
        }
        
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return (false, "Invalid username or password", null, null);
        }
        
        if (!VerifyPassword(password, account.PasswordHash))
        {
            return (false, "Invalid username or password", null, null);
        }
        
        // Create session token
        var sessionToken = GenerateSessionToken();
        
        lock (account)
        {
            // Remove old session from index
            if (!string.IsNullOrEmpty(account.SessionToken))
            {
                _sessionIndex.TryRemove(account.SessionToken, out _);
            }
            
            account.SessionToken = sessionToken;
            account.SessionExpiresAt = DateTime.UtcNow.AddDays(SESSION_EXPIRY_DAYS);
        }
        
        _sessionIndex[sessionToken] = accountId;
        SaveAccounts();
        
        _logger.LogInformation("[ACCOUNT] Login: {Username}", username);
        
        return (true, null, account, sessionToken);
    }

    /// <summary>
    /// Gets an account by username.
    /// </summary>
    public Account? GetAccountByUsername(string username)
    {
        username = username.Trim().ToLowerInvariant();
        
        if (!_usernameIndex.TryGetValue(username, out var accountId))
        {
            return null;
        }
        
        _accounts.TryGetValue(accountId, out var account);
        return account;
    }

    public Account? GetAccountById(string accountId)
    {
        _accounts.TryGetValue(accountId, out var account);
        return account;
    }

    /// <summary>
    /// Gets an account by session token.
    /// </summary>
    public Account? GetAccountBySession(string sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return null;
        }
        
        if (!_sessionIndex.TryGetValue(sessionToken, out var accountId))
        {
            return null;
        }
        
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return null;
        }
        
        // Check if session is expired
        if (account.SessionExpiresAt.HasValue && account.SessionExpiresAt.Value < DateTime.UtcNow)
        {
            Logout(sessionToken);
            return null;
        }
        
        return account;
    }

    /// <summary>
    /// Logs out a session.
    /// </summary>
    public void Logout(string sessionToken)
    {
        if (_sessionIndex.TryRemove(sessionToken, out var accountId))
        {
            if (_accounts.TryGetValue(accountId, out var account))
            {
                lock (account)
                {
                    if (account.SessionToken == sessionToken)
                    {
                        account.SessionToken = null;
                        account.SessionExpiresAt = null;
                    }
                }
                SaveAccounts();
            }
        }
    }

    /// <summary>
    /// Generates a verification code for linking a YouTube channel.
    /// </summary>
    public string GenerateYouTubeLinkCode(string accountId)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return "";
        }
        
        var code = "LINK-" + GenerateRandomCode(4);
        
        lock (account)
        {
            account.PendingVerificationCode = code;
            account.VerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(VERIFICATION_CODE_EXPIRY_MINUTES);
        }
        
        SaveAccounts();
        _logger.LogInformation("[ACCOUNT] Generated link code {Code} for {Username}", code, account.Username);
        
        return code;
    }

    /// <summary>
    /// Checks if a chat message (regular or Super Chat) contains a verification code and links the channel.
    /// Called by YouTubeService when processing any chat message.
    /// </summary>
    public (bool Linked, string? AccountId) CheckAndLinkFromChatMessage(string youtubeChannelId, string message, string displayName)
    {
        // Check if this YouTube channel is already linked
        if (_youtubeIndex.ContainsKey(youtubeChannelId))
        {
            // Already linked, just return the account ID
            return (false, _youtubeIndex[youtubeChannelId]);
        }
        
        _logger.LogDebug("[ACCOUNT] Checking message for link code: '{Message}'", message);
        
        // Search for a matching verification code in any account
        foreach (var account in _accounts.Values)
        {
            if (string.IsNullOrEmpty(account.PendingVerificationCode))
                continue;
                
            if (account.VerificationCodeExpiresAt.HasValue && account.VerificationCodeExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogDebug("[ACCOUNT] Skipping expired code for {Username}: {Code}", 
                    account.Username, account.PendingVerificationCode);
                continue;
            }
            
            _logger.LogDebug("[ACCOUNT] Checking against code {Code} for {Username}", 
                account.PendingVerificationCode, account.Username);
            
            if (message.Contains(account.PendingVerificationCode, StringComparison.OrdinalIgnoreCase))
            {
                // Found a match! Link the channel
                LinkChannelToAccount(account, youtubeChannelId, displayName);
                
                _logger.LogInformation("[ACCOUNT] ✓ Linked YouTube channel {ChannelId} to {Username} via chat message", 
                    youtubeChannelId, account.Username);
                
                return (true, account.Id);
            }
        }
        
        return (false, null);
    }
    
    /// <summary>
    /// Links a channel to an account and transfers any pending credits.
    /// </summary>
    private void LinkChannelToAccount(Account account, string youtubeChannelId, string displayName, string? pictureUrl = null)
    {
        decimal transferredCredits = 0;
        
        lock (account)
        {
            account.LinkedYouTubeChannelId = youtubeChannelId;
            account.PendingVerificationCode = null;
            account.VerificationCodeExpiresAt = null;
            
            // Update display name if not set
            if (account.DisplayName == account.Username && !string.IsNullOrEmpty(displayName))
            {
                account.DisplayName = displayName;
            }
            
            // Set picture URL if provided
            if (!string.IsNullOrEmpty(pictureUrl))
            {
                account.PictureUrl = pictureUrl;
            }
            
            // Transfer any pending credits for this channel
            if (_pendingCredits.TryRemove(youtubeChannelId, out var pending))
            {
                account.CreditBalance += pending.PendingBalance;
                transferredCredits = pending.PendingBalance;
                _logger.LogInformation("[ACCOUNT] Transferred ${Amount} pending credits to {Username}", 
                    transferredCredits, account.Username);
            }
        }
        
        _youtubeIndex[youtubeChannelId] = account.Id;
        SaveAccounts();
        SavePendingCredits();
        
        // Fire event for UI notification
        OnChannelLinked?.Invoke(youtubeChannelId, account.Username);
    }

    /// <summary>
    /// Adds credits to a YouTube channel. If linked to an account, adds directly.
    /// If not linked, stores as pending credits that will transfer when linked.
    /// </summary>
    public void AddCreditsToChannel(string youtubeChannelId, decimal amount, string displayName, string? message = null)
    {
        // Check if this channel is linked to an account
        if (_youtubeIndex.TryGetValue(youtubeChannelId, out var accountId))
        {
            // Add credits directly to the account
            AddCredits(accountId, amount);
            return;
        }
        
        // Store as pending credits
        var pending = _pendingCredits.GetOrAdd(youtubeChannelId, _ => new PendingChannelCredits
        {
            ChannelId = youtubeChannelId,
            DisplayName = displayName
        });
        
        lock (pending)
        {
            pending.PendingBalance += amount;
            pending.DisplayName = displayName; // Update display name
            pending.Donations.Add(new DonationRecord
            {
                Timestamp = DateTime.UtcNow,
                Amount = amount,
                Source = "superchat",
                Message = message
            });
        }
        
        SavePendingCredits();
        _logger.LogInformation("[ACCOUNT] Stored ${Amount} pending credits for unlinked channel {ChannelId} ({DisplayName})", 
            amount, youtubeChannelId, displayName);
    }
    
    /// <summary>
    /// Gets pending credits for a YouTube channel.
    /// </summary>
    public decimal GetPendingCreditsForChannel(string youtubeChannelId)
    {
        if (_pendingCredits.TryGetValue(youtubeChannelId, out var pending))
        {
            return pending.PendingBalance;
        }
        return 0;
    }

    /// <summary>
    /// Directly links a YouTube channel to an account (used for OAuth linking).
    /// </summary>
    public bool LinkYouTubeChannel(string accountId, string youtubeChannelId, string? pictureUrl = null)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return false;
        }
        
        // Check if this YouTube channel is already linked to another account
        if (_youtubeIndex.ContainsKey(youtubeChannelId))
        {
            return false;
        }
        
        // Use the helper to link and transfer pending credits
        LinkChannelToAccount(account, youtubeChannelId, "", pictureUrl);
        
        _logger.LogInformation("[ACCOUNT] Linked YouTube channel {ChannelId} to account {AccountId}", 
            youtubeChannelId, accountId);
        
        return true;
    }

    /// <summary>
    /// Links a YouTube channel via Google Sign-In (JWT verification).
    /// </summary>
    public bool LinkYouTubeChannelViaGoogle(string accountId, string googleCredential, string? expectedClientId)
    {
        // Verify the JWT token
        var (googleId, pictureUrl) = VerifyGoogleToken(googleCredential, expectedClientId);
        
        if (string.IsNullOrEmpty(googleId))
        {
            _logger.LogWarning("[ACCOUNT] Failed to verify Google credential for account {AccountId}", accountId);
            return false;
        }
        
        // Link the Google ID as the YouTube channel ID
        return LinkYouTubeChannel(accountId, googleId, pictureUrl);
    }

    /// <summary>
    /// Verifies a Google ID token and returns the Google ID (sub claim) and picture URL if valid.
    /// </summary>
    private (string? GoogleId, string? PictureUrl) VerifyGoogleToken(string credential, string? expectedClientId)
    {
        try
        {
            // Simple JWT decode and verification
            var parts = credential.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("[AUTH] Invalid JWT format");
                return (null, null);
            }
            
            // Decode payload
            var payload = parts[1];
            // Add padding if needed
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            
            var jsonPayload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var claims = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonPayload);
            
            if (claims == null)
            {
                _logger.LogWarning("[AUTH] Failed to parse JWT claims");
                return (null, null);
            }
            
            // Verify issuer
            if (claims.TryGetValue("iss", out var issObj))
            {
                var iss = issObj?.ToString();
                if (iss != "https://accounts.google.com" && iss != "accounts.google.com")
                {
                    _logger.LogWarning("[AUTH] Invalid JWT issuer: {Issuer}", iss);
                    return (null, null);
                }
            }
            else
            {
                _logger.LogWarning("[AUTH] Missing JWT issuer");
                return (null, null);
            }
            
            // Verify audience (client ID) if provided
            if (!string.IsNullOrEmpty(expectedClientId) && claims.TryGetValue("aud", out var audObj))
            {
                var aud = audObj?.ToString();
                if (aud != expectedClientId)
                {
                    _logger.LogWarning("[AUTH] JWT audience mismatch. Expected: {Expected}, Got: {Got}", 
                        expectedClientId, aud);
                    return (null, null);
                }
            }
            
            // Verify expiration
            if (claims.TryGetValue("exp", out var expObj))
            {
                if (expObj is System.Text.Json.JsonElement jsonElement && jsonElement.TryGetInt64(out var exp))
                {
                    var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                    if (expTime < DateTimeOffset.UtcNow)
                    {
                        _logger.LogWarning("[AUTH] JWT token expired");
                        return (null, null);
                    }
                }
            }
            
            // Extract Google ID (sub claim)
            string? googleId = null;
            string? pictureUrl = null;
            
            if (claims.TryGetValue("sub", out var subObj))
            {
                googleId = subObj?.ToString();
            }
            
            // Extract picture URL
            if (claims.TryGetValue("picture", out var pictureObj))
            {
                pictureUrl = pictureObj?.ToString();
            }
            
            if (string.IsNullOrEmpty(googleId))
            {
                _logger.LogWarning("[AUTH] Missing sub claim in JWT");
                return (null, null);
            }
            
            _logger.LogInformation("[AUTH] Verified Google token for Google ID: {GoogleId}", googleId);
            return (googleId, pictureUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AUTH] Failed to verify Google token");
            return (null, null);
        }
    }

    /// <summary>
    /// Gets an account by YouTube Channel ID.
    /// </summary>
    public Account? GetAccountByYouTubeChannel(string youtubeChannelId)
    {
        if (_youtubeIndex.TryGetValue(youtubeChannelId, out var accountId))
        {
            _accounts.TryGetValue(accountId, out var account);
            return account;
        }
        return null;
    }

    /// <summary>
    /// Adds credits to an account.
    /// </summary>
    public void AddCredits(string accountId, decimal amount)
    {
        if (_accounts.TryGetValue(accountId, out var account))
        {
            lock (account)
            {
                account.CreditBalance += amount;
            }
            SaveAccounts();
            _logger.LogInformation("[ACCOUNT] Added ${Amount} to {Username}. Balance: ${Balance}",
                amount, account.Username, account.CreditBalance);
        }
    }

    /// <summary>
    /// Deducts credits from an account.
    /// </summary>
    public bool DeductCredits(string accountId, decimal amount)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return false;
        }

        lock (account)
        {
            if (account.CreditBalance < amount)
            {
                return false;
            }

            account.CreditBalance -= amount;
            account.TotalSpent += amount;
            account.LastRequestTime = DateTime.UtcNow;
        }

        SaveAccounts();
        return true;
    }

    /// <summary>
    /// Checks rate limit for an account.
    /// </summary>
    public (bool Allowed, double WaitSeconds) CheckRateLimit(string accountId)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return (true, 0);
        }

        var timeSinceLast = DateTime.UtcNow - account.LastRequestTime;
        if (timeSinceLast.TotalSeconds < DEFAULT_RATE_LIMIT_SECONDS)
        {
            return (false, DEFAULT_RATE_LIMIT_SECONDS - timeSinceLast.TotalSeconds);
        }

        return (true, 0);
    }

    /// <summary>
    /// Gets all accounts (for admin user management).
    /// </summary>
    public List<Account> GetAllAccounts()
    {
        return _accounts.Values
            .Where(a => a.Id != ANONYMOUS_USER_ID)
            .OrderBy(a => a.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Updates a user's role (admin only).
    /// </summary>
    public bool UpdateUserRole(string accountId, UserRole newRole)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return false;
        }
        
        account.Role = newRole;
        SaveAccounts();
        
        _logger.LogInformation("[ACCOUNT] Updated role for {Username} to {Role}", account.Username, newRole);
        return true;
    }

    /// <summary>
    /// Deletes a user account (admin only).
    /// </summary>
    public bool DeleteAccount(string accountId)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return false;
        }

        // Remove from all indices
        _usernameIndex.TryRemove(account.Username, out _);
        if (!string.IsNullOrEmpty(account.LinkedYouTubeChannelId))
        {
            _youtubeIndex.TryRemove(account.LinkedYouTubeChannelId, out _);
        }
        if (!string.IsNullOrEmpty(account.SessionToken))
        {
            _sessionIndex.TryRemove(account.SessionToken, out _);
        }
        
        _accounts.TryRemove(accountId, out _);
        SaveAccounts();
        
        _logger.LogInformation("[ACCOUNT] Deleted account: {Username} ({Id})", account.Username, accountId);
        return true;
    }
    
    /// <summary>
    /// Submits a chaos command with full validation, credit deduction, and moderation handling.
    /// Returns (success, message, commandId, newBalance).
    /// </summary>
    public async Task<(bool Success, string Message, int? CommandId, decimal NewBalance)> SubmitChaosCommandAsync(
        string accountId,
        string prompt,
        Func<string, Task<(string ExecutionCode, string UndoCode, bool NeedsModeration, string? ModerationReason)>> codeGenerator,
        Func<string, bool> needsModeration,
        Func<string, List<string>> extractImageUrls,
        Action<string, string, string, string, string?, int> addPendingImage,
        Action<string, string, string, string, string, string, string?, int?> addPendingCode,
        Func<string, string, string, string, string, string?, string?, string?, CommandStatus, bool, CommandEntry> addCommandWithStatus,
        bool isPrivateDiscordMode,
        bool isTestClientModeEnabled,
        bool isSingleUserMode = false)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return (false, "Account not found", null, 0);
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return (false, "Prompt required", null, account.CreditBalance);
        }

        // Skip rate limit check in single user mode
        if (!isSingleUserMode)
        {
            // Check rate limit
            var (allowed, waitSeconds) = CheckRateLimit(accountId);
            if (!allowed)
            {
                return (false, $"Please wait {waitSeconds:F0} seconds before submitting another command.", null, account.CreditBalance);
            }

            // Check credits
            if (account.CreditBalance < Constants.CommandCost)
            {
                return (false, $"Insufficient credits. You have ${account.CreditBalance:F2}, but need ${Constants.CommandCost:F2}", null, account.CreditBalance);
            }
        }

        // Check for images in the prompt - queue for moderation if found (skip if Private Discord Mode)
        if (!isPrivateDiscordMode && needsModeration(prompt))
        {
            _logger.LogInformation("[SUBMIT] User {Username} submitting command with images. Balance before: ${Balance}", 
                account.Username, account.CreditBalance);
            
            // Deduct credits NOW (before moderation) - skip in single user mode
            if (!isSingleUserMode && !DeductCredits(accountId, Constants.CommandCost))
            {
                _logger.LogError("[SUBMIT] Failed to deduct credits from {Username}", account.Username);
                return (false, "Failed to deduct credits", null, account.CreditBalance);
            }
            
            var updatedAccount = _accounts[accountId];
            if (!isSingleUserMode)
            {
                _logger.LogInformation("[SUBMIT] Credits deducted. Balance after: ${Balance}", updatedAccount.CreditBalance);
            }
            
            // Create placeholder command in history with PendingModeration status
            var placeholderCommand = addCommandWithStatus(
                prompt,
                "", // No code yet
                "", // No undo yet
                "web",
                account.DisplayName,
                null, // Image URL will be added when approved
                accountId,
                "⏳ Waiting for image moderation approval...",
                CommandStatus.PendingModeration,
                false); // Don't queue yet
            
            _logger.LogInformation("[SUBMIT] Created placeholder command #{CommandId} with status {Status}", 
                placeholderCommand.Id, placeholderCommand.Status);
            
            // Queue images for moderation with link to command
            var imageUrls = extractImageUrls(prompt);
            foreach (var url in imageUrls)
            {
                addPendingImage(url, prompt, "web", account.DisplayName, accountId, placeholderCommand.Id);
            }

            _logger.LogInformation("[MODERATION] Command #{CommandId} with {Count} image(s) queued for review from {Username}",
                placeholderCommand.Id, imageUrls.Count, account.Username);

            var moderationMessage = isSingleUserMode 
                ? $"Your command contains {imageUrls.Count} image(s) that require moderator approval."
                : $"Your command contains {imageUrls.Count} image(s) that require moderator approval. Credits have been deducted and will be refunded if denied.";

            return (true, 
                moderationMessage,
                placeholderCommand.Id,
                updatedAccount.CreditBalance);
        }

        // Generate code
        var (executionCode, undoCode, codeNeedsModeration, moderationReason) = await codeGenerator(prompt);

        // Check if the generated code needs moderation (URLs, external requests, etc.)
        if (!isPrivateDiscordMode && codeNeedsModeration)
        {
            _logger.LogInformation("[SUBMIT] User {Username} submitting command with filtered code. Reason: {Reason}", 
                account.Username, moderationReason);
            
            // Deduct credits NOW (before moderation) - skip in single user mode
            if (!isSingleUserMode && !DeductCredits(accountId, Constants.CommandCost))
            {
                _logger.LogError("[SUBMIT] Failed to deduct credits from {Username}", account.Username);
                return (false, "Failed to deduct credits", null, account.CreditBalance);
            }
            
            var updatedAccount = _accounts[accountId];
            if (!isSingleUserMode)
            {
                _logger.LogInformation("[SUBMIT] Credits deducted for code moderation. Balance after: ${Balance}", updatedAccount.CreditBalance);
            }
            
            // Create placeholder command in history with PendingModeration status
            var placeholderCommand = addCommandWithStatus(
                prompt,
                executionCode,
                undoCode,
                "web",
                account.DisplayName,
                null,
                accountId,
                $"⚠️ Code contains filtered content: {moderationReason}. Waiting for moderator approval...",
                CommandStatus.PendingModeration,
                false); // Don't queue yet
            
            _logger.LogInformation("[CODE MODERATION] Created placeholder command #{CommandId} with status {Status}", 
                placeholderCommand.Id, placeholderCommand.Status);
            
            // Add to code moderation queue
            addPendingCode(prompt, executionCode, undoCode, moderationReason, "web", account.DisplayName, accountId, placeholderCommand.Id);
            
            _logger.LogInformation("[CODE MODERATION] Command #{CommandId} queued for review from {Username} (Reason: {Reason})",
                placeholderCommand.Id, account.Username, moderationReason);

            var codeModerationMessage = isSingleUserMode 
                ? $"Your code contains filtered content ({moderationReason}) and requires moderator approval."
                : $"Your code contains filtered content ({moderationReason}) and requires moderator approval. Credits have been deducted and will be refunded if denied.";

            return (true, 
                codeModerationMessage,
                placeholderCommand.Id,
                updatedAccount.CreditBalance);
        }

        // Deduct credits - skip in single user mode
        if (!isSingleUserMode && !DeductCredits(accountId, Constants.CommandCost))
        {
            return (false, "Failed to deduct credits", null, account.CreditBalance);
        }

        // Add to command queue
        var command = addCommandWithStatus(
            prompt,
            executionCode,
            undoCode,
            "web",
            account.DisplayName,
            null,
            accountId,
            null,
            CommandStatus.Queued,
            !isTestClientModeEnabled); // Queue for execution if test client is disabled

        var finalAccount = _accounts[accountId];
        return (true, "Command submitted successfully", command.Id, finalAccount.CreditBalance);
    }
    
    /// <summary>
    /// Submits an interactive command with validation, credit deduction, and moderation handling.
    /// Unlike regular commands, this doesn't generate code yet - that happens in the interactive session.
    /// Returns (success, message, commandId, newBalance).
    /// </summary>
    public async Task<(bool Success, string Message, int? CommandId, decimal NewBalance)> SubmitInteractiveCommandAsync(
        string accountId,
        string prompt,
        Func<string, bool> needsModeration,
        Func<string, List<string>> extractImageUrls,
        Action<string, string, string, string, string?, int> addPendingImage,
        Func<string, string, string, string, string, string?, string?, string?, CommandStatus, bool, CommandEntry> addCommandWithStatus,
        bool isPrivateDiscordMode,
        bool isSingleUserMode = false)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return (false, "Account not found", null, 0);
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return (false, "Prompt required", null, account.CreditBalance);
        }

        // Skip rate limit and credit checks in single user mode
        if (!isSingleUserMode)
        {
            // Check rate limit
            var (allowed, waitSeconds) = CheckRateLimit(accountId);
            if (!allowed)
            {
                return (false, $"Please wait {waitSeconds:F0} seconds before submitting another command.", null, account.CreditBalance);
            }

            // Check credits
            if (account.CreditBalance < Constants.CommandCost)
            {
                return (false, $"Insufficient credits. You have ${account.CreditBalance:F2}, but need ${Constants.CommandCost:F2}", null, account.CreditBalance);
            }
        }

        // Check for images in the prompt - queue for moderation if found (skip if Private Discord Mode)
        if (!isPrivateDiscordMode && needsModeration(prompt))
        {
            _logger.LogInformation("[INTERACTIVE] User {Username} submitting interactive command with images. Balance before: ${Balance}", 
                account.Username, account.CreditBalance);
            
            // Deduct credits NOW (before moderation) - skip in single user mode
            if (!isSingleUserMode && !DeductCredits(accountId, Constants.CommandCost))
            {
                _logger.LogError("[INTERACTIVE] Failed to deduct credits from {Username}", account.Username);
                return (false, "Failed to deduct credits", null, account.CreditBalance);
            }
            
            var updatedAccount = _accounts[accountId];
            if (!isSingleUserMode)
            {
                _logger.LogInformation("[INTERACTIVE] Credits deducted. Balance after: ${Balance}", updatedAccount.CreditBalance);
            }
            
            // Create placeholder command in history with PendingModeration status
            var placeholderCommand = addCommandWithStatus(
                prompt,
                "", // No code yet
                "", // No undo yet
                "web",
                account.DisplayName,
                null, // Image URL will be added when approved
                accountId,
                "⏳ [Interactive Mode] Waiting for image moderation approval...",
                CommandStatus.PendingModeration,
                false); // Don't queue yet
            
            _logger.LogInformation("[INTERACTIVE] Created placeholder command #{CommandId} with status {Status}", 
                placeholderCommand.Id, placeholderCommand.Status);
            
            // Queue images for moderation with link to command
            var imageUrls = extractImageUrls(prompt);
            foreach (var url in imageUrls)
            {
                addPendingImage(url, prompt, "web", account.DisplayName, accountId, placeholderCommand.Id);
            }

            _logger.LogInformation("[INTERACTIVE] Command #{CommandId} with {Count} image(s) queued for review from {Username}",
                placeholderCommand.Id, imageUrls.Count, account.Username);

            var moderationMessage = isSingleUserMode
                ? $"Your interactive command contains {imageUrls.Count} image(s) that require moderator approval."
                : $"Your interactive command contains {imageUrls.Count} image(s) that require moderator approval. Credits have been deducted and will be refunded if denied.";

            return (true, 
                moderationMessage,
                placeholderCommand.Id,
                updatedAccount.CreditBalance);
        }

        // For interactive mode without images, just deduct credits and let the session proceed
        // The interactive session will handle code generation
        // Skip credit deduction in single user mode
        if (!isSingleUserMode && !DeductCredits(accountId, Constants.CommandCost))
        {
            return (false, "Failed to deduct credits", null, account.CreditBalance);
        }

        var finalAccount = _accounts[accountId];
        if (!isSingleUserMode)
        {
            _logger.LogInformation("[INTERACTIVE] Credits deducted for interactive session. Balance: ${Balance}", finalAccount.CreditBalance);
        }
        else
        {
            _logger.LogInformation("[INTERACTIVE] Starting interactive session (single user mode - no credits required)");
        }
        
        var message = isSingleUserMode 
            ? "Starting interactive session" 
            : "Credits deducted - starting interactive session";
        
        return (true, message, null, finalAccount.CreditBalance);
    }
    
    /// <summary>
    /// Gets an account by username or account ID.
    /// </summary>
    public Account? GetAccount(string usernameOrId)
    {
        // Try as account ID first
        if (_accounts.TryGetValue(usernameOrId, out var account))
        {
            return account;
        }
        
        // Try as username (case-insensitive)
        var username = usernameOrId.Trim().ToLowerInvariant();
        if (_usernameIndex.TryGetValue(username, out var accountId))
        {
            _accounts.TryGetValue(accountId, out account);
            return account;
        }
        
        return null;
    }

    /// <summary>
    /// Gets all pending credits (for admin to see who has pending donations).
    /// </summary>
    public List<PendingChannelCredits> GetAllPendingCredits()
    {
        return _pendingCredits.Values.ToList();
    }
    
    /// <summary>
    /// Manually links a pending channel's credits to an account.
    /// Used when a user has pending credits but their YouTube OAuth gave us a Google ID instead of channel ID.
    /// </summary>
    public bool ManuallyLinkPendingCredits(string accountId, string youtubeChannelId)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            _logger.LogWarning("[ACCOUNT] Cannot manually link - account {AccountId} not found", accountId);
            return false;
        }
        
        if (!_pendingCredits.TryGetValue(youtubeChannelId, out var pending))
        {
            _logger.LogWarning("[ACCOUNT] Cannot manually link - no pending credits for channel {ChannelId}", youtubeChannelId);
            return false;
        }
        
        // Check if this channel is already linked
        if (_youtubeIndex.ContainsKey(youtubeChannelId))
        {
            var existingAccountId = _youtubeIndex[youtubeChannelId];
            if (existingAccountId != accountId)
            {
                _logger.LogWarning("[ACCOUNT] Cannot manually link - channel {ChannelId} already linked to account {ExistingAccountId}", 
                    youtubeChannelId, existingAccountId);
                return false;
            }
        }
        
        // Transfer the pending credits
        lock (account)
        {
            account.CreditBalance += pending.PendingBalance;
            
            // Update the account's linked channel if it's different
            if (account.LinkedYouTubeChannelId != youtubeChannelId)
            {
                // Remove old channel from index if present
                if (!string.IsNullOrEmpty(account.LinkedYouTubeChannelId))
                {
                    _youtubeIndex.TryRemove(account.LinkedYouTubeChannelId, out _);
                }
                
                account.LinkedYouTubeChannelId = youtubeChannelId;
                _youtubeIndex[youtubeChannelId] = account.Id;
            }
            
            _logger.LogInformation("[ACCOUNT] Manually transferred ${Amount} pending credits from channel {ChannelId} to {Username}", 
                pending.PendingBalance, youtubeChannelId, account.Username);
        }
        
        // Remove from pending
        _pendingCredits.TryRemove(youtubeChannelId, out _);
        
        SaveAccounts();
        SavePendingCredits();
        
        return true;
    }
    
    /// <summary>
    /// Detects accounts that have Google IDs instead of YouTube Channel IDs.
    /// YouTube Channel IDs typically start with "UC" and are 24 characters long.
    /// Google IDs are numeric strings.
    /// </summary>
    public List<Account> GetAccountsWithIncorrectYouTubeIds()
    {
        var accountsWithIssues = new List<Account>();
        
        foreach (var account in _accounts.Values)
        {
            if (string.IsNullOrEmpty(account.LinkedYouTubeChannelId))
                continue;
                
            var channelId = account.LinkedYouTubeChannelId;
            
            // YouTube Channel IDs typically:
            // - Start with "UC" for user channels
            // - Are exactly 24 characters long
            // - Contain alphanumeric characters, hyphens, and underscores
            //
            // Google IDs (sub claim from JWT):
            // - Are numeric strings (all digits)
            // - Are typically 21 characters long
            
            bool looksLikeGoogleId = channelId.All(char.IsDigit);
            bool looksLikeYouTubeChannelId = channelId.StartsWith("UC") && channelId.Length == 24;
            
            // Flag as incorrect if it looks like a Google ID AND doesn't look like a YouTube Channel ID
            if (looksLikeGoogleId && !looksLikeYouTubeChannelId)
            {
                accountsWithIssues.Add(account);
                _logger.LogInformation("[ACCOUNT] Account {Username} ({AccountId}) has incorrect YouTube ID: {ChannelId} (looks like Google ID)",
                    account.Username, account.Id, channelId);
            }
        }
        
        return accountsWithIssues;
    }
    
    /// <summary>
    /// Unlinks YouTube channel from an account.
    /// This allows the user to relink with the correct YouTube Channel ID.
    /// </summary>
    public bool UnlinkYouTubeChannel(string accountId)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            return false;
        }
        
        if (string.IsNullOrEmpty(account.LinkedYouTubeChannelId))
        {
            _logger.LogWarning("[ACCOUNT] Account {Username} has no linked YouTube channel to unlink", account.Username);
            return false;
        }
        
        var oldChannelId = account.LinkedYouTubeChannelId;
        
        lock (account)
        {
            // Remove from YouTube index
            _youtubeIndex.TryRemove(oldChannelId, out _);
            
            // Clear the linked channel
            account.LinkedYouTubeChannelId = null;
            
            _logger.LogInformation("[ACCOUNT] Unlinked YouTube channel {ChannelId} from account {Username}", 
                oldChannelId, account.Username);
        }
        
        SaveAccounts();
        return true;
    }
    
    /// <summary>
    /// Unlinks all accounts that have incorrect YouTube IDs (Google IDs instead of YouTube Channel IDs).
    /// This allows users to relink properly with the correct YouTube Channel IDs.
    /// Returns the count of unlinked accounts.
    /// </summary>
    public int UnlinkAccountsWithIncorrectYouTubeIds()
    {
        var accountsToUnlink = GetAccountsWithIncorrectYouTubeIds();
        
        _logger.LogInformation("[ACCOUNT] Found {Count} accounts with incorrect YouTube IDs to unlink", accountsToUnlink.Count);
        
        int unlinkedCount = 0;
        foreach (var account in accountsToUnlink)
        {
            if (UnlinkYouTubeChannel(account.Id))
            {
                unlinkedCount++;
            }
        }
        
        _logger.LogInformation("[ACCOUNT] Successfully unlinked {Count} accounts with incorrect YouTube IDs", unlinkedCount);
        return unlinkedCount;
    }

    private static string HashPassword(string password)
    {
        // Use PBKDF2 with a random salt for secure password hashing
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);
        
        // Combine salt + hash for storage
        var combined = new byte[salt.Length + hash.Length];
        Array.Copy(salt, 0, combined, 0, salt.Length);
        Array.Copy(hash, 0, combined, salt.Length, hash.Length);
        
        return Convert.ToBase64String(combined);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var combined = Convert.FromBase64String(storedHash);
            if (combined.Length < 48) return false; // 16 (salt) + 32 (hash)
            
            var salt = new byte[16];
            var storedHashBytes = new byte[32];
            Array.Copy(combined, 0, salt, 0, 16);
            Array.Copy(combined, 16, storedHashBytes, 0, 32);
            
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            var computedHash = pbkdf2.GetBytes(32);
            
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHashBytes);
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateSessionToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = new char[length];
        for (int i = 0; i < length; i++)
        {
            code[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }
        return new string(code);
    }

    private void LoadAccounts()
    {
        try
        {
            if (File.Exists(_accountsPath))
            {
                var json = File.ReadAllText(_accountsPath);
                var accounts = JsonSerializer.Deserialize<List<Account>>(json);

                if (accounts != null)
                {
                    foreach (var account in accounts)
                    {
                        _accounts[account.Id] = account;
                        _usernameIndex[account.Username] = account.Id;
                        
                        if (!string.IsNullOrEmpty(account.LinkedYouTubeChannelId))
                        {
                            _youtubeIndex[account.LinkedYouTubeChannelId] = account.Id;
                        }
                        
                        if (!string.IsNullOrEmpty(account.SessionToken) && 
                            account.SessionExpiresAt.HasValue && 
                            account.SessionExpiresAt.Value > DateTime.UtcNow)
                        {
                            _sessionIndex[account.SessionToken] = account.Id;
                        }
                    }
                    _logger.LogInformation("Loaded {Count} accounts from disk", _accounts.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load accounts");
        }
    }

    private void SaveAccounts()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_accounts.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_accountsPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save accounts");
            }
        }
    }
    
    private void LoadPendingCredits()
    {
        try
        {
            if (File.Exists(_pendingCreditsPath))
            {
                var json = File.ReadAllText(_pendingCreditsPath);
                var credits = JsonSerializer.Deserialize<List<PendingChannelCredits>>(json);

                if (credits != null)
                {
                    foreach (var pending in credits)
                    {
                        _pendingCredits[pending.ChannelId] = pending;
                    }
                    _logger.LogInformation("Loaded {Count} pending credit records from disk", _pendingCredits.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pending credits");
        }
    }
    
    private void SavePendingCredits()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_pendingCredits.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_pendingCreditsPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save pending credits");
            }
        }
    }
}
