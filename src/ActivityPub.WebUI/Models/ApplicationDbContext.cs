using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ActivityPub.WebUI.Models;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(e => e.DisplayName);
            entity.HasIndex(e => e.ActorId).IsUnique();
            entity.Property(e => e.DisplayName).HasMaxLength(50);
            entity.Property(e => e.Bio).HasMaxLength(500);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.ActorId).HasMaxLength(500);
        });
    }
}
