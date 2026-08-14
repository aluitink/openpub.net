using Microsoft.EntityFrameworkCore;
using ActivityPub.Core.Repositories;

namespace ActivityPub.Core.Repositories;

public class ActivityPubDbContext : DbContext
{
    public DbSet<ActorEntity> Actors { get; set; } = null!;
    public DbSet<ActivityEntity> Activities { get; set; } = null!;
    public DbSet<SharedInboxDeliveryEntity> SharedInboxDeliveries { get; set; } = null!;

    public ActivityPubDbContext(DbContextOptions<ActivityPubDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActorEntity>()
            .HasKey(a => a.Id);

        modelBuilder.Entity<ActorEntity>()
            .Property(a => a.Username)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<ActorEntity>()
            .HasIndex(a => a.Username)
            .IsUnique();

        modelBuilder.Entity<ActorEntity>()
            .Property(a => a.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<ActorEntity>()
            .Property(a => a.UpdatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<ActivityEntity>()
            .HasKey(a => a.Id);

        modelBuilder.Entity<ActivityEntity>()
            .Property(a => a.ActivityId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<ActivityEntity>()
            .HasIndex(a => a.ActivityId)
            .IsUnique();

        modelBuilder.Entity<ActivityEntity>()
            .Property(a => a.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<ActivityEntity>()
            .Property(a => a.UpdatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<SharedInboxDeliveryEntity>()
            .HasKey(d => d.Id);

        modelBuilder.Entity<SharedInboxDeliveryEntity>()
            .Property(d => d.ActivityId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<SharedInboxDeliveryEntity>()
            .Property(d => d.TargetActorId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<SharedInboxDeliveryEntity>()
            .Property(d => d.Status)
            .HasDefaultValue(DeliveryStatus.Queued);

        modelBuilder.Entity<SharedInboxDeliveryEntity>()
            .Property(d => d.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<SharedInboxDeliveryEntity>()
            .Property(d => d.UpdatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<SharedInboxDeliveryEntity>()
            .HasIndex(d => d.Status);

        modelBuilder.Entity<SharedInboxDeliveryEntity>()
            .HasIndex(d => new { d.ActivityId, d.TargetActorId })
            .IsUnique();
    }
}
