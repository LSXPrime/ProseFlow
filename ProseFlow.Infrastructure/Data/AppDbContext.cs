using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProseFlow.Core.Abstracts;
using ProseFlow.Core.Models;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Action> Actions { get; set; }
    public DbSet<ActionGroup> ActionGroups { get; set; }
    public DbSet<ActionPlaceholder> ActionPlaceholders { get; set; }
    public DbSet<GeneralSettings> GeneralSettings { get; set; }
    public DbSet<ProviderSettings> ProviderSettings { get; set; }
    public DbSet<CloudProviderConfiguration> CloudProviderConfigurations { get; set; }
    public DbSet<HistoryEntry> History { get; set; }
    public DbSet<UsageStatistic> UsageStatistics { get; set; }
    public DbSet<LocalModel> LocalModels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure singleton settings tables by seeding them with a default entity.
        modelBuilder.Entity<GeneralSettings>().HasData(new GeneralSettings { Id = 1, IsOnboardingCompleted = false });
        modelBuilder.Entity<ProviderSettings>().HasData(new ProviderSettings { Id = 1 });

        // Seed a default "General" group that cannot be deleted.
        modelBuilder.Entity<ActionGroup>().HasData(new ActionGroup { Id = 1, Name = "General", SortOrder = 0 });

        // Configure the Action entity
        modelBuilder.Entity<Action>(entity =>
        {
            // Configure the relationship with ActionGroup
            entity.HasOne(a => a.ActionGroup)
                .WithMany(g => g.Actions)
                .HasForeignKey(a => a.ActionGroupId)
                .IsRequired();
            
            // Set the default value for ActionGroupId
            entity.Property(a => a.ActionGroupId)
                .HasDefaultValue(1);

            // Configure the one-to-many relationship with ActionPlaceholder
            entity.HasMany(a => a.Placeholders)
                .WithOne(p => p.Action)
                .HasForeignKey(p => p.ActionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Define a value comparer for the List<string> to ensure EF Core can track changes correctly.
            var valueComparer = new ValueComparer<List<string>>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());
            
            // Configure the List<string> to be stored as a JSON string in the database.
            entity.Property(a => a.ApplicationContext)
                .HasConversion(
                    // Convert List<string> to a JSON string for storage
                    list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
                    // Convert JSON string back to List<string> when reading
                    jsonString => JsonSerializer.Deserialize<List<string>>(jsonString, (JsonSerializerOptions?)null) ??
                                  new List<string>())
                .Metadata.SetValueComparer(valueComparer);
        });
        
        // Ensure Year and Month are a unique combination for usage statistics.
        modelBuilder.Entity<UsageStatistic>()
            .HasIndex(u => new { u.Year, u.Month })
            .IsUnique();

        // Ensure file paths for local models are unique.
        modelBuilder.Entity<LocalModel>()
            .HasIndex(m => m.FilePath)
            .IsUnique();
    }
    
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e is { Entity: EntityBase, State: EntityState.Added or EntityState.Modified });

        foreach (var entityEntry in entries)
        {
            var entity = (EntityBase)entityEntry.Entity;
            var now = DateTimeOffset.UtcNow;

            if (entityEntry.State == EntityState.Added)
                entity.CreatedAtUtc = now;
            else
                entityEntry.Property(nameof(EntityBase.CreatedAtUtc)).IsModified = false;

            entity.UpdatedAtUtc = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}