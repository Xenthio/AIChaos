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

    public AccountService(ILogger<AccountService> logger)
    {
        _logger = logger;
        _accountsPath = Path.Combine(AppContext.BaseDirectory, "accounts.json");
        _pendingCreditsPath = Path.Combine(AppContext.BaseDirectory, "pending_credits.json");
        LoadAccounts();
        LoadPendingCredits();
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
        
        var account = new Account
        {
            Username = username,
            PasswordHash = HashPassword(password),
            DisplayName = displayName ?? username,
            CreatedAt = DateTime.UtcNow
        };
        
        _accounts[account.Id] = account;
        _usernameIndex[username] = account.Id;
        SaveAccounts();
        
        _logger.LogInformation("[ACCOUNT] Created account: {Username} ({Id})", username, account.Id);
        
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
        
        // Search for a matching verification code in any account
        foreach (var account in _accounts.Values)
        {
            if (string.IsNullOrEmpty(account.PendingVerificationCode))
                continue;
                
            if (account.VerificationCodeExpiresAt.HasValue && account.VerificationCodeExpiresAt.Value < DateTime.UtcNow)
                continue;
            
            if (message.Contains(account.PendingVerificationCode, StringComparison.OrdinalIgnoreCase))
            {
                // Found a match! Link the channel
                LinkChannelToAccount(account, youtubeChannelId, displayName);
                
                _logger.LogInformation("[ACCOUNT] Linked YouTube channel {ChannelId} to {Username} via chat message", 
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
