using System.Collections.Concurrent;
using System.Text.Json;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for managing users, credits, and rate limits.
/// </summary>
public class UserService
{
    private readonly string _usersPath;
    private readonly ILogger<UserService> _logger;
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly object _lock = new();

    // Configurable settings (could be moved to SettingsService later)
    private const int DEFAULT_RATE_LIMIT_SECONDS = 20;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
        _usersPath = Path.Combine(AppContext.BaseDirectory, "users.json");
        LoadUsers();
    }

    /// <summary>
    /// Gets a user by ID, creating them if they don't exist.
    /// </summary>
    public User GetOrCreateUser(string userId, string displayName, string platform = "youtube")
    {
        return _users.GetOrAdd(userId, id =>
        {
            var user = new User
            {
                Id = id,
                DisplayName = displayName,
                Platform = platform,
                CreditBalance = 0
            };
            SaveUsers(); // Save immediately on creation
            return user;
        });
    }

    /// <summary>
    /// Gets a user by ID if they exist.
    /// </summary>
    public User? GetUser(string userId)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            return user;
        }
        return null;
    }

    /// <summary>
    /// Adds credits to a user's balance.
    /// </summary>
    public void AddCredits(string userId, decimal amount, string displayName)
    {
        var user = GetOrCreateUser(userId, displayName);

        lock (user)
        {
            user.CreditBalance += amount;
            // Update display name in case it changed
            user.DisplayName = displayName;
        }

        SaveUsers();
        _logger.LogInformation("[USER] Added ${Amount} to {User} ({Id}). New Balance: ${Balance}",
            amount, displayName, userId, user.CreditBalance);
    }

    /// <summary>
    /// Deducts credits from a user's balance.
    /// Returns true if successful, false if insufficient funds.
    /// </summary>
    public bool DeductCredits(string userId, decimal amount)
    {
        if (!_users.TryGetValue(userId, out var user))
        {
            return false;
        }

        lock (user)
        {
            if (user.CreditBalance < amount)
            {
                return false;
            }

            user.CreditBalance -= amount;
            user.TotalSpent += amount;
            user.LastRequestTime = DateTime.UtcNow;
        }

        SaveUsers();
        _logger.LogInformation("[USER] Deducted ${Amount} from {User} ({Id}). New Balance: ${Balance}",
            amount, user.DisplayName, userId, user.CreditBalance);

        return true;
    }

    /// <summary>
    /// Checks if a user can submit a request based on rate limits.
    /// </summary>
    public (bool Allowed, double WaitSeconds) CheckRateLimit(string userId)
    {
        if (!_users.TryGetValue(userId, out var user))
        {
            return (true, 0);
        }

        var timeSinceLast = DateTime.UtcNow - user.LastRequestTime;
        if (timeSinceLast.TotalSeconds < DEFAULT_RATE_LIMIT_SECONDS)
        {
            return (false, DEFAULT_RATE_LIMIT_SECONDS - timeSinceLast.TotalSeconds);
        }

        return (true, 0);
    }

    private void LoadUsers()
    {
        try
        {
            if (File.Exists(_usersPath))
            {
                var json = File.ReadAllText(_usersPath);
                var users = JsonSerializer.Deserialize<List<User>>(json);

                if (users != null)
                {
                    foreach (var user in users)
                    {
                        _users.TryAdd(user.Id, user);
                    }
                    _logger.LogInformation("Loaded {Count} users from disk", _users.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users");
        }
    }

    private void SaveUsers()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_users.Values, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_usersPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save users");
            }
        }
    }
}
