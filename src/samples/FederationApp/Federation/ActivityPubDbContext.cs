using ActivityPub.Core;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FederationApp.Federation;

public class ActivityPubDbContext : DbContext
{
    public DbSet<Instance> Instances { get; set; } = null!;

    public ActivityPubDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Instance>()
            .HasKey(i => i.Id);

        modelBuilder.Entity<Instance>()
            .Property(i => i.Domain)
            .IsRequired();

        modelBuilder.Entity<Instance>()
            .HasIndex(i => i.Domain)
            .IsUnique();
    }
}
