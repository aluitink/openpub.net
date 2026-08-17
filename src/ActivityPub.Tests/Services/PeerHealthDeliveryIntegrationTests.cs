using ActivityPub.Core.Caching;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Phase 38 Task 3 — peer health integration with the delivery and inbound
/// paths. Verifies that <see cref="SharedInboxService.ProcessQueueAsync"/> skips
/// deliveries to blocked peers (recording no failure and not contacting the
/// outbound sender), and that
/// <see cref="SharedInboxService.ProcessAndDistributeActivityAsync"/> rejects
/// inbound activities originating from blocked peers.
/// </summary>
public class PeerHealthDeliveryIntegrationTests
{
    private const string ActivityJson =
        """{"id":"urn:activity:1","type":"Create","actor":"https://local.example/users/alice#main-key"}""";
    private const string Target = "https://remote.example/users/bob#main-key";

    private static SharedInboxService CreateService(
        Mock<IOutboundActivityService> outbound,
        InMemoryActivityPubRepository repo,
        PeerHealthService peerHealth,
        PeerHealthOptions peerHealthOptions)
    {
        var options = new ActivityPubOptions
        {
            DeliveryRetry = new DeliveryRetryOptions(),
            PeerHealth = peerHealthOptions
        };
        return new SharedInboxService(
            repo,
            outbound.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IFederationCache>(),
            Mock.Of<ILogger<SharedInboxService>>(),
            Options.Create(options),
            peerHealth);
    }

    private static PeerHealthService CreatePeerHealth(InMemoryActivityPubRepository repo, PeerHealthOptions options)
    {
        var ap = new ActivityPubOptions { PeerHealth = options };
        return new PeerHealthService(
            repo,
            Options.Create(ap),
            Mock.Of<ILogger<PeerHealthService>>());
    }

    /// <summary>
    /// Seeds a local sender actor (username "alice") with a private key into
    /// the repository so that <c>SharedInboxService</c> can sign outbound
    /// deliveries. The actor ID matches the one referenced by
    /// <see cref="ActivityJson"/>.
    /// </summary>
    private static async Task SeedSenderActorAsync(InMemoryActivityPubRepository repo)
    {
        var actor = new Actor
        {
            Id = "https://local.example/users/alice#main-key",
            PreferredUsername = "alice",
            AdditionalProperties = new System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>
            {
                ["privateKeyPem"] = System.Text.Json.JsonSerializer.SerializeToElement("test-private-key-pem")
            }
        };
        await repo.SaveUserActorAsync(actor);
    }

    [Fact]
    public async Task ProcessQueueAsync_SkipsDelivery_ToBlockedPeer()
    {
        var repo = new InMemoryActivityPubRepository();
        var peerHealth = CreatePeerHealth(repo, new PeerHealthOptions { AutoBlockThreshold = 1 });
        var outbound = new Mock<IOutboundActivityService>();
        outbound
            .Setup(s => s.SendActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Block the target domain.
        await peerHealth.BlockDomainAsync("remote.example");

        var service = CreateService(outbound, repo, peerHealth, new PeerHealthOptions());

        Assert.True(await repo.QueueSharedInboxDeliveryAsync("act-blocked", ActivityJson, Target));
        // Capture the entity reference the repository holds; because
        // UpdateSharedInboxDeliveryAsync mutates the same instance, this is a
        // stable handle to the row's final state even when the item is
        // backoff-gated out of the pending query.
        var item = Assert.Single(await repo.GetPendingSharedInboxDeliveriesAsync(10, 10), d => d.ActivityId == "act-blocked");
        await service.ProcessQueueAsync();

        // The delivery was NOT sent to the outbound service.
        outbound.Verify(
            s => s.SendActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        // And it is left in a retry-able Failed state (not Delivered), backoff-gated
        // by a future NextRetryAt so the queue processor will not re-attempt it
        // immediately (it may be unblocked by then).
        Assert.Equal(DeliveryStatus.Failed, item.Status);
        Assert.NotNull(item.NextRetryAt);
        Assert.True(item.NextRetryAt > DateTime.UtcNow);
        Assert.Contains("blocked", item.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessQueueAsync_RecordsFailure_AutoBlocksPeer_AtThreshold()
    {
        var repo = new InMemoryActivityPubRepository();
        await SeedSenderActorAsync(repo);
        var peerHealth = CreatePeerHealth(repo, new PeerHealthOptions { AutoBlockThreshold = 2 });
        var outbound = new Mock<IOutboundActivityService>();
        outbound
            .Setup(s => s.SendActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false); // every delivery fails

        var service = CreateService(outbound, repo, peerHealth, new PeerHealthOptions { AutoBlockThreshold = 2 });

        Assert.True(await repo.QueueSharedInboxDeliveryAsync("act-fail", ActivityJson, Target));

        // First failure: not yet blocked (threshold is 2).
        await service.ProcessQueueAsync();
        Assert.False(await peerHealth.IsDomainBlockedAsync("remote.example"));

        // Re-queue (simulate a new delivery to the same peer) and fail again.
        await repo.QueueSharedInboxDeliveryAsync("act-fail-2", ActivityJson, Target);
        await service.ProcessQueueAsync();

        // Now blocked (2 consecutive failures).
        Assert.True(await peerHealth.IsDomainBlockedAsync("remote.example"));
    }

    [Fact]
    public async Task ProcessAndDistribute_RejectsInboundActivity_FromBlockedPeer()
    {
        var repo = new InMemoryActivityPubRepository();
        var peerHealth = CreatePeerHealth(repo, new PeerHealthOptions());
        var outbound = new Mock<IOutboundActivityService>();

        // Block the origin domain of the inbound actor.
        await peerHealth.BlockDomainAsync("bad.example");

        var service = CreateService(outbound, repo, peerHealth, new PeerHealthOptions());

        var activity = new Activity
        {
            Id = "urn:inbound:1",
            Type = "Create",
            Actor = "https://bad.example/users/evil"
        };

        var result = await service.ProcessAndDistributeActivityAsync("alice", activity);

        Assert.False(result);
        // The activity must NOT have been saved.
        var saved = await repo.GetActivityAsync("urn:inbound:1");
        Assert.Null(saved);
    }

    [Fact]
    public async Task ProcessAndDistribute_AcceptsInboundActivity_FromUnblockedPeer()
    {
        var repo = new InMemoryActivityPubRepository();
        var peerHealth = CreatePeerHealth(repo, new PeerHealthOptions());
        var outbound = new Mock<IOutboundActivityService>();

        var service = CreateService(outbound, repo, peerHealth, new PeerHealthOptions());

        var activity = new Activity
        {
            Id = "urn:inbound:2",
            Type = "Create",
            Actor = "https://good.example/users/legit"
        };

        var result = await service.ProcessAndDistributeActivityAsync("alice", activity);

        Assert.True(result);
        var saved = await repo.GetActivityAsync("urn:inbound:2");
        Assert.NotNull(saved);
    }
}
