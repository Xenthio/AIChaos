using Microsoft.EntityFrameworkCore;
using AIChaos.Brain.Models;

namespace AIChaos.Brain.Data;

/// <summary>
/// Entity Framework Core DbContext for the AIChaos application.
/// Manages database operations for accounts, settings, and pending credits.
/// </summary>
public class AIChaosDbContext : DbContext
{
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<AppSettings> Settings { get; set; } = null!;
    public DbSet<PendingChannelCredits> PendingCredits { get; set; } = null!;

    public AIChaosDbContext(DbContextOptions<AIChaosDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Account entity
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.Username).IsUnique();
            entity.HasIndex(a => a.LinkedYouTubeChannelId);
            entity.HasIndex(a => a.SessionToken);
            
            entity.Property(a => a.Username).IsRequired().HasMaxLength(100);
            entity.Property(a => a.PasswordHash).IsRequired();
            entity.Property(a => a.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(a => a.CreditBalance).HasPrecision(18, 2);
            entity.Property(a => a.TotalSpent).HasPrecision(18, 2);
        });

        // Configure AppSettings entity
        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.ToTable("Settings");
            
            // Store complex properties as JSON
            entity.OwnsOne(s => s.General);
            entity.OwnsOne(s => s.OpenRouter);
            entity.OwnsOne(s => s.Twitch);
            entity.OwnsOne(s => s.YouTube);
            entity.OwnsOne(s => s.Admin);
            entity.OwnsOne(s => s.Tunnel);
            entity.OwnsOne(s => s.Safety);
            entity.OwnsOne(s => s.TestClient);
            entity.OwnsOne(s => s.StreamState);
        });

        // Configure PendingChannelCredits entity
        modelBuilder.Entity<PendingChannelCredits>(entity =>
        {
            entity.HasKey(p => p.ChannelId);
            entity.Property(p => p.PendingBalance).HasPrecision(18, 2);
            entity.OwnsMany(p => p.Donations);
        });
    }
}
