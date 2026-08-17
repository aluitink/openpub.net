using Microsoft.EntityFrameworkCore;
using ActivityPub.Core.Repositories;

namespace ActivityPub.Core.Repositories;

public class ActivityPubDbContext : DbContext
{
    public DbSet<ActorEntity> Actors { get; set; } = null!;
    public DbSet<ActivityEntity> Activities { get; set; } = null!;
    public DbSet<SharedInboxDeliveryEntity> SharedInboxDeliveries { get; set; } = null!;
    public DbSet<InboxDeadLetterEntity> InboxDeadLetters { get; set; } = null!;
    public DbSet<WebhookConfigEntity> WebhookConfigs { get; set; } = null!;
    public DbSet<WebhookDeliveryEntity> WebhookDeliveries { get; set; } = null!;
    public DbSet<WebhookDeliveryHistoryEntity> WebhookDeliveryHistories { get; set; } = null!;
    public DbSet<OAuth2AuthorizationCodeEntity> AuthorizationCodes { get; set; } = null!;
    public DbSet<OAuth2AccessTokenEntity> AccessTokens { get; set; } = null!;
    public DbSet<OAuth2RefreshTokenEntity> RefreshTokens { get; set; } = null!;
    public DbSet<OAuthClientEntity> OAuthClients { get; set; } = null!;
    public DbSet<OAuthCodeEntity> OAuthCodes { get; set; } = null!;
    public DbSet<OAuthTokenEntity> OAuthTokens { get; set; } = null!;
    public DbSet<UserPreferenceEntity> UserPreferences { get; set; } = null!;
    public DbSet<CommunityEntity> Communities { get; set; } = null!;
    public DbSet<CommunityMemberEntity> CommunityMembers { get; set; } = null!;
    public DbSet<FederationPeerEntity> FederationPeers { get; set; } = null!;

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

        modelBuilder.Entity<ActivityEntity>()
            .HasIndex(a => a.CreatedAt);

        modelBuilder.Entity<ActivityEntity>()
            .HasIndex(a => new { a.ActivityId, a.CreatedAt });

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

        modelBuilder.Entity<InboxDeadLetterEntity>()
            .HasKey(d => d.Id);

        modelBuilder.Entity<InboxDeadLetterEntity>()
            .Property(d => d.ActivityId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<InboxDeadLetterEntity>()
            .Property(d => d.Username)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<InboxDeadLetterEntity>()
            .Property(d => d.Status)
            .HasDefaultValue(InboxDeadLetterStatus.DeadLettered);

        modelBuilder.Entity<InboxDeadLetterEntity>()
            .Property(d => d.FailureReason)
            .HasMaxLength(1000);

        modelBuilder.Entity<InboxDeadLetterEntity>()
            .Property(d => d.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<InboxDeadLetterEntity>()
            .Property(d => d.UpdatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<InboxDeadLetterEntity>()
            .HasIndex(d => d.Status);

        modelBuilder.Entity<InboxDeadLetterEntity>()
            .HasIndex(d => new { d.ActivityId, d.Username });

        modelBuilder.Entity<WebhookConfigEntity>()
            .HasKey(c => c.Id);

        modelBuilder.Entity<WebhookConfigEntity>()
            .Property(c => c.ActorId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<WebhookConfigEntity>()
            .Property(c => c.EndpointUrl)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<WebhookConfigEntity>()
            .Property(c => c.HttpMethod)
            .IsRequired()
            .HasMaxLength(10);

        modelBuilder.Entity<WebhookConfigEntity>()
            .Property(c => c.EventType)
            .IsRequired()
            .HasMaxLength(50);

        modelBuilder.Entity<WebhookConfigEntity>()
            .Property(c => c.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<WebhookConfigEntity>()
            .Property(c => c.UpdatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<WebhookConfigEntity>()
            .HasIndex(c => new { c.ActorId, c.EventType })
            .IsUnique(false);

        modelBuilder.Entity<WebhookDeliveryEntity>()
            .HasKey(d => d.Id);

        modelBuilder.Entity<WebhookDeliveryEntity>()
            .Property(d => d.ConfigId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<WebhookDeliveryEntity>()
            .Property(d => d.ActivityId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<WebhookDeliveryEntity>()
            .Property(d => d.ActorId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<WebhookDeliveryEntity>()
            .Property(d => d.Status)
            .HasDefaultValue(WebhookDeliveryStatus.Queued);

        modelBuilder.Entity<WebhookDeliveryEntity>()
            .Property(d => d.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<WebhookDeliveryEntity>()
            .Property(d => d.UpdatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<WebhookDeliveryEntity>()
            .HasIndex(d => d.Status);

        modelBuilder.Entity<WebhookDeliveryEntity>()
            .HasIndex(d => d.ConfigId);

        modelBuilder.Entity<WebhookDeliveryHistoryEntity>()
            .HasKey(h => h.Id);

        modelBuilder.Entity<WebhookDeliveryHistoryEntity>()
            .Property(h => h.DeliveryId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<WebhookDeliveryHistoryEntity>()
            .Property(h => h.EventType)
            .IsRequired()
            .HasMaxLength(50);

        modelBuilder.Entity<WebhookDeliveryHistoryEntity>()
            .Property(h => h.RequestHeaders)
            .IsRequired();

        modelBuilder.Entity<WebhookDeliveryHistoryEntity>()
            .Property(h => h.RequestBody)
            .IsRequired();

        modelBuilder.Entity<WebhookDeliveryHistoryEntity>()
            .Property(h => h.ResponseHeaders)
            .IsRequired();

        modelBuilder.Entity<WebhookDeliveryHistoryEntity>()
            .Property(h => h.ResponseBody)
            .IsRequired();

        modelBuilder.Entity<WebhookDeliveryHistoryEntity>()
            .HasIndex(h => h.DeliveryId);

        modelBuilder.Entity<OAuth2AuthorizationCodeEntity>()
            .HasKey(a => a.Id);

        modelBuilder.Entity<OAuth2AuthorizationCodeEntity>()
            .Property(a => a.Code)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuth2AuthorizationCodeEntity>()
            .HasIndex(a => a.Code)
            .IsUnique();

        modelBuilder.Entity<OAuth2AuthorizationCodeEntity>()
            .Property(a => a.ClientId)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuth2AuthorizationCodeEntity>()
            .Property(a => a.Scopes)
            .IsRequired();

        modelBuilder.Entity<OAuth2AuthorizationCodeEntity>()
            .HasOne(a => a.Actor)
            .WithMany()
            .HasForeignKey(a => a.ActorId);

        modelBuilder.Entity<OAuth2AccessTokenEntity>()
            .HasKey(a => a.Id);

        modelBuilder.Entity<OAuth2AccessTokenEntity>()
            .Property(a => a.Token)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuth2AccessTokenEntity>()
            .HasIndex(a => a.Token)
            .IsUnique();

        modelBuilder.Entity<OAuth2AccessTokenEntity>()
            .Property(a => a.ClientId)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuth2AccessTokenEntity>()
            .Property(a => a.Scopes)
            .IsRequired();

        modelBuilder.Entity<OAuth2AccessTokenEntity>()
            .HasOne(a => a.Actor)
            .WithMany()
            .HasForeignKey(a => a.ActorId);

        modelBuilder.Entity<OAuth2RefreshTokenEntity>()
            .HasKey(r => r.Id);

        modelBuilder.Entity<OAuth2RefreshTokenEntity>()
            .Property(r => r.Token)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuth2RefreshTokenEntity>()
            .HasIndex(r => r.Token)
            .IsUnique();

        modelBuilder.Entity<OAuth2RefreshTokenEntity>()
            .Property(r => r.ClientId)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuth2RefreshTokenEntity>()
            .HasOne(r => r.Actor)
            .WithMany()
            .HasForeignKey(r => r.ActorId);

        modelBuilder.Entity<OAuthClientEntity>()
            .HasKey(c => c.Id);

        modelBuilder.Entity<OAuthClientEntity>()
            .Property(c => c.ClientId)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthClientEntity>()
            .HasIndex(c => c.ClientId)
            .IsUnique();

        modelBuilder.Entity<OAuthClientEntity>()
            .Property(c => c.ClientSecret)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthClientEntity>()
            .Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthClientEntity>()
            .Property(c => c.Scopes)
            .IsRequired();

        modelBuilder.Entity<OAuthClientEntity>()
            .Property(c => c.RedirectUris)
            .IsRequired();

        modelBuilder.Entity<OAuthClientEntity>()
            .Property(c => c.Website)
            .HasMaxLength(500);

        modelBuilder.Entity<OAuthClientEntity>()
            .Property(c => c.OwnerActorId)
            .HasMaxLength(500);

        modelBuilder.Entity<OAuthClientEntity>()
            .HasIndex(c => c.OwnerActorId);

        modelBuilder.Entity<OAuthCodeEntity>()
            .HasKey(c => c.Id);

        modelBuilder.Entity<OAuthCodeEntity>()
            .Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthCodeEntity>()
            .HasIndex(c => c.Code)
            .IsUnique();

        modelBuilder.Entity<OAuthCodeEntity>()
            .Property(c => c.Username)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthCodeEntity>()
            .Property(c => c.ClientId)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthCodeEntity>()
            .Property(c => c.Scopes)
            .IsRequired();

        modelBuilder.Entity<OAuthCodeEntity>()
            .Property(c => c.CodeChallenge)
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthCodeEntity>()
            .HasIndex(c => new { c.Username, c.ClientId });

        modelBuilder.Entity<OAuthTokenEntity>()
            .HasKey(t => t.Id);

        modelBuilder.Entity<OAuthTokenEntity>()
            .Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthTokenEntity>()
            .HasIndex(t => t.Token)
            .IsUnique();

        modelBuilder.Entity<OAuthTokenEntity>()
            .Property(t => t.Username)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthTokenEntity>()
            .Property(t => t.ClientId)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<OAuthTokenEntity>()
            .Property(t => t.Scopes)
            .IsRequired();

        modelBuilder.Entity<OAuthTokenEntity>()
            .HasIndex(t => t.Username);

        modelBuilder.Entity<UserPreferenceEntity>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<UserPreferenceEntity>()
            .Property(p => p.Key)
            .IsRequired()
            .HasMaxLength(50);

        modelBuilder.Entity<UserPreferenceEntity>()
            .Property(p => p.Value)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<UserPreferenceEntity>()
            .HasIndex(p => new { p.ActorId, p.Key, p.Value })
            .IsUnique();

        modelBuilder.Entity<UserPreferenceEntity>()
            .HasOne(p => p.Actor)
            .WithMany()
            .HasForeignKey(p => p.ActorId);

        modelBuilder.Entity<CommunityEntity>()
            .HasKey(c => c.Id);

        modelBuilder.Entity<CommunityEntity>()
            .Property(c => c.CommunityId)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<CommunityEntity>()
            .HasIndex(c => c.CommunityId)
            .IsUnique();

        modelBuilder.Entity<CommunityEntity>()
            .Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<CommunityEntity>()
            .Property(c => c.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<CommunityEntity>()
            .Property(c => c.UpdatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<CommunityEntity>()
            .HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerActorId);

        modelBuilder.Entity<CommunityMemberEntity>()
            .HasKey(m => m.Id);

        modelBuilder.Entity<CommunityMemberEntity>()
            .HasIndex(m => new { m.CommunityId, m.ActorId })
            .IsUnique();

        modelBuilder.Entity<CommunityMemberEntity>()
            .Property(m => m.JoinedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<CommunityMemberEntity>()
            .HasOne(m => m.Community)
            .WithMany()
            .HasForeignKey(m => m.CommunityId);

        modelBuilder.Entity<CommunityMemberEntity>()
            .HasOne(m => m.Actor)
            .WithMany()
            .HasForeignKey(m => m.ActorId);

        modelBuilder.Entity<FederationPeerEntity>()
            .HasKey(p => p.Domain);

        modelBuilder.Entity<FederationPeerEntity>()
            .Property(p => p.Domain)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<FederationPeerEntity>()
            .Property(p => p.BlockedReason)
            .HasMaxLength(500);

        modelBuilder.Entity<FederationPeerEntity>()
            .Property(p => p.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<FederationPeerEntity>()
            .Property(p => p.UpdatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<FederationPeerEntity>()
            .HasIndex(p => p.IsBlocked);
    }
}
