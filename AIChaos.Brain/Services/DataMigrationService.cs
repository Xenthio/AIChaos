using System.Text.Json;
using AIChaos.Brain.Data;
using AIChaos.Brain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for migrating data from JSON files to SQLite database.
/// Provides one-time migration and backup functionality.
/// </summary>
public class DataMigrationService
{
    private readonly AIChaosDbContext _dbContext;
    private readonly ILogger<DataMigrationService> _logger;
    private readonly string _accountsPath;
    private readonly string _settingsPath;
    private readonly string _pendingCreditsPath;

    public DataMigrationService(
        AIChaosDbContext dbContext,
        ILogger<DataMigrationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _accountsPath = Path.Combine(AppContext.BaseDirectory, "accounts.json");
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        _pendingCreditsPath = Path.Combine(AppContext.BaseDirectory, "pending_credits.json");
    }

    /// <summary>
    /// Migrates all data from JSON files to SQLite database.
    /// Only migrates if database is empty and JSON files exist.
    /// </summary>
    public async Task<bool> MigrateFromJsonIfNeededAsync()
    {
        try
        {
            // Check if database already has data
            var hasAccounts = await _dbContext.Accounts.AnyAsync();
            var hasSettings = await _dbContext.Settings.AnyAsync();
            
            if (hasAccounts || hasSettings)
            {
                _logger.LogInformation("[Migration] Database already contains data, skipping JSON migration");
                return false;
            }

            // Check if JSON files exist
            var hasJsonData = File.Exists(_accountsPath) || File.Exists(_settingsPath);
            if (!hasJsonData)
            {
                _logger.LogInformation("[Migration] No JSON files found, initializing fresh database");
                return false;
            }

            _logger.LogInformation("[Migration] Starting migration from JSON files to SQLite...");

            // Migrate accounts
            if (File.Exists(_accountsPath))
            {
                await MigrateAccountsAsync();
            }

            // Migrate settings
            if (File.Exists(_settingsPath))
            {
                await MigrateSettingsAsync();
            }

            // Migrate pending credits
            if (File.Exists(_pendingCreditsPath))
            {
                await MigratePendingCreditsAsync();
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("[Migration] Successfully migrated all data from JSON to SQLite");
            
            // Create backup of JSON files
            BackupJsonFiles();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Migration] Failed to migrate data from JSON to SQLite");
            return false;
        }
    }

    private async Task MigrateAccountsAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_accountsPath);
            var accounts = JsonSerializer.Deserialize<List<Account>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (accounts != null && accounts.Count > 0)
            {
                await _dbContext.Accounts.AddRangeAsync(accounts);
                _logger.LogInformation("[Migration] Migrated {Count} accounts from JSON", accounts.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Migration] Failed to migrate accounts from JSON");
        }
    }

    private async Task MigrateSettingsAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (settings != null)
            {
                // Ensure the settings has the default ID
                settings.Id = 1;
                await _dbContext.Settings.AddAsync(settings);
                _logger.LogInformation("[Migration] Migrated settings from JSON");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Migration] Failed to migrate settings from JSON");
        }
    }

    private async Task MigratePendingCreditsAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_pendingCreditsPath);
            var pendingCredits = JsonSerializer.Deserialize<List<PendingChannelCredits>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (pendingCredits != null && pendingCredits.Count > 0)
            {
                await _dbContext.PendingCredits.AddRangeAsync(pendingCredits);
                _logger.LogInformation("[Migration] Migrated {Count} pending credit records from JSON", pendingCredits.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Migration] Failed to migrate pending credits from JSON");
        }
    }

    private void BackupJsonFiles()
    {
        try
        {
            var backupDir = Path.Combine(AppContext.BaseDirectory, "json_backup");
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            if (File.Exists(_accountsPath))
            {
                var backupPath = Path.Combine(backupDir, $"accounts_{timestamp}.json");
                File.Copy(_accountsPath, backupPath);
                _logger.LogInformation("[Migration] Backed up accounts.json to {Path}", backupPath);
            }

            if (File.Exists(_settingsPath))
            {
                var backupPath = Path.Combine(backupDir, $"settings_{timestamp}.json");
                File.Copy(_settingsPath, backupPath);
                _logger.LogInformation("[Migration] Backed up settings.json to {Path}", backupPath);
            }

            if (File.Exists(_pendingCreditsPath))
            {
                var backupPath = Path.Combine(backupDir, $"pending_credits_{timestamp}.json");
                File.Copy(_pendingCreditsPath, backupPath);
                _logger.LogInformation("[Migration] Backed up pending_credits.json to {Path}", backupPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Migration] Failed to backup JSON files (non-critical)");
        }
    }

    /// <summary>
    /// Exports current database data back to JSON files (for backup/rollback purposes).
    /// </summary>
    public async Task ExportToJsonAsync()
    {
        try
        {
            _logger.LogInformation("[Migration] Exporting database to JSON files...");

            // Export accounts
            var accounts = await _dbContext.Accounts.ToListAsync();
            var accountsJson = JsonSerializer.Serialize(accounts, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_accountsPath, accountsJson);
            _logger.LogInformation("[Migration] Exported {Count} accounts to JSON", accounts.Count);

            // Export settings
            var settings = await _dbContext.Settings.FirstOrDefaultAsync();
            if (settings != null)
            {
                var settingsJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_settingsPath, settingsJson);
                _logger.LogInformation("[Migration] Exported settings to JSON");
            }

            // Export pending credits
            var pendingCredits = await _dbContext.PendingCredits.ToListAsync();
            var pendingCreditsJson = JsonSerializer.Serialize(pendingCredits, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_pendingCreditsPath, pendingCreditsJson);
            _logger.LogInformation("[Migration] Exported {Count} pending credit records to JSON", pendingCredits.Count);

            _logger.LogInformation("[Migration] Successfully exported all data to JSON");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Migration] Failed to export database to JSON");
        }
    }
}
