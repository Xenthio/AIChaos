# Database Migration Guide

This document explains the Entity Framework Core database migration that has been implemented in AIChaos.

## Overview

AIChaos now uses SQLite with Entity Framework Core for data persistence instead of JSON files. The migration is **automatic and backward compatible**.

## What Changed?

### Database Location
- Database file: `aichaos.db` (created in the application directory)
- JSON files are no longer used by default, but are backed up automatically

### Automatic Migration

When you start the application for the first time after updating:

1. **Database Creation**: A new SQLite database (`aichaos.db`) is created automatically
2. **Data Migration**: If existing JSON files are found (`accounts.json`, `settings.json`, `pending_credits.json`):
   - All data is automatically migrated to the database
   - JSON files are backed up to `json_backup/` directory with timestamps
   - Original JSON files remain untouched as backups
3. **Future Startups**: The application uses the SQLite database going forward

### Database Structure

The database contains the following tables:

- **Accounts**: User accounts with credentials, balances, and YouTube linking
- **Settings**: Application settings (OpenRouter, Twitch, YouTube, etc.)
- **PendingCredits**: YouTube channel credits waiting to be linked
- **DonationRecords**: Individual donation records for pending credits

## Backward Compatibility

### JSON Backup
- All JSON files are backed up to `json_backup/` before migration
- Backup files are timestamped (e.g., `accounts_20241214_120000.json`)
- Original JSON files are preserved

### Rollback Option
If you need to roll back to JSON files (not recommended):

1. Stop the application
2. Delete or rename `aichaos.db`
3. The application will use JSON files again

Note: Currently, services still use JSON files. A future update will migrate services to use EF Core directly.

## Benefits of SQLite

### Performance
- Faster queries and data access
- Better concurrent access handling
- Efficient indexing for lookups

### Reliability
- ACID compliance (Atomicity, Consistency, Isolation, Durability)
- Reduced risk of data corruption
- Automatic transaction management

### Scalability
- Better handling of large datasets
- Optimized queries with Entity Framework LINQ
- Proper relationships between entities

## Technical Details

### Migration Process
1. On startup, the application checks if the database is empty
2. If empty and JSON files exist, it reads and migrates all data
3. Data is inserted into the database with all relationships intact
4. JSON files are backed up for safety
5. Migration is logged for troubleshooting

### Database Migrations
The application uses EF Core migrations for schema changes:
- Migrations are automatically applied on startup
- No manual database setup required
- Schema changes are version controlled

## Troubleshooting

### Migration Logs
Check the application logs for migration status:
- `[Migration] Starting migration from JSON files to SQLite...`
- `[Migration] Migrated X accounts from JSON`
- `[Migration] Successfully migrated all data from JSON to SQLite`

### Common Issues

**Issue**: Migration doesn't happen
- **Cause**: Database already has data, or no JSON files exist
- **Solution**: This is normal behavior. Check logs for details.

**Issue**: Data missing after migration
- **Cause**: JSON file corruption or parsing error
- **Solution**: Check logs for errors. Restore from `json_backup/` if needed.

**Issue**: Application won't start
- **Cause**: Database file permission issues
- **Solution**: Ensure the application has write permissions to its directory.

## Future Enhancements

The following improvements are planned for future releases:

1. **Service Migration**: Update AccountService and SettingsService to use EF Core directly
2. **Performance Optimization**: Add caching and query optimization
3. **Additional Indexes**: Optimize for common query patterns
4. **Audit Logging**: Track all database changes for security

## For Developers

### DbContext
The `AIChaosDbContext` class manages database access:
```csharp
public class AIChaosDbContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AppSettings> Settings { get; set; }
    public DbSet<PendingChannelCredits> PendingCredits { get; set; }
}
```

### DataMigrationService
The `DataMigrationService` handles JSON↔SQLite conversion:
- `MigrateFromJsonIfNeededAsync()`: Auto-migration on startup
- `ExportToJsonAsync()`: Export database to JSON (for backup/debugging)

### Creating New Migrations
When modifying entity models:
```bash
dotnet ef migrations add YourMigrationName --project AIChaos.Brain
```

The migration will be automatically applied on next startup.

## Support

If you encounter any issues with the database migration:

1. Check application logs for detailed error messages
2. Verify `json_backup/` contains your data
3. Report issues on GitHub with relevant log excerpts

---

**Note**: This migration is part of the architectural improvements to enhance performance, reliability, and scalability of AIChaos.
