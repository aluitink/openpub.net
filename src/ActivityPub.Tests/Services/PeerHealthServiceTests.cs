using ActivityPub.Core.Implementations;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Phase 38 Task 3 — federation peer health tracking and auto-blocking.
/// Exercises <see cref="PeerHealthService"/> against the in-memory repository,
/// verifying auto-block on consecutive failures, auto-unblock on consecutive
/// successes, liveness-probe auto-block, and the manual block/unblock paths.
/// </summary>
public class PeerHealthServiceTests
{
    private static PeerHealthService CreateService(
        InMemoryActivityPubRepository repo,
        PeerHealthOptions options)
    {
        var ap = new ActivityPubOptions { PeerHealth = options };
        return new PeerHealthService(
            repo,
            Options.Create(ap),
            Mock.Of<ILogger<PeerHealthService>>());
    }

    [Fact]
    public async Task ConsecutiveFailures_AutoBlockPeer_AtThreshold()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions { AutoBlockThreshold = 3 });

        Assert.False(await service.RecordDeliveryOutcomeAsync("bad.example", success: false));
        Assert.False(await service.RecordDeliveryOutcomeAsync("bad.example", success: false));
        var blocked = await service.RecordDeliveryOutcomeAsync("bad.example", success: false);

        Assert.True(blocked);
        var peer = await repo.GetFederationPeerAsync("bad.example");
        Assert.NotNull(peer);
        Assert.True(peer!.IsBlocked);
        Assert.Equal(3, peer.ConsecutiveFailures);
        Assert.NotEqual(null, peer.BlockedAt);
    }

    [Fact]
    public async Task Success_ResetsFailureStreak_DoesNotBlock()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions { AutoBlockThreshold = 3 });

        await service.RecordDeliveryOutcomeAsync("flaky.example", success: false);
        await service.RecordDeliveryOutcomeAsync("flaky.example", success: false);
        await service.RecordDeliveryOutcomeAsync("flaky.example", success: true); // reset
        await service.RecordDeliveryOutcomeAsync("flaky.example", success: false);
        await service.RecordDeliveryOutcomeAsync("flaky.example", success: false);

        // Only 2 consecutive failures at this point (the streak was reset by the
        // success in between), so we are below the threshold of 3.
        Assert.False(await service.IsDomainBlockedAsync("flaky.example"));
        var peer = await repo.GetFederationPeerAsync("flaky.example");
        Assert.Equal(2, peer!.ConsecutiveFailures);
        Assert.Equal(0, peer.ConsecutiveSuccesses); // reset by the last failure
        Assert.Equal(1, peer.TotalDeliveries == 5 ? 1 : 0); // sanity: 5 total outcomes
        Assert.Equal(5, peer.TotalDeliveries);
    }

    [Fact]
    public async Task BlockedPeer_AutoUnblocks_AfterConsecutiveSuccesses()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions
        {
            AutoBlockThreshold = 2,
            AutoUnblockSuccessThreshold = 2
        });

        // Block it.
        await service.RecordDeliveryOutcomeAsync("recovering.example", success: false);
        await service.RecordDeliveryOutcomeAsync("recovering.example", success: false);
        Assert.True(await service.IsDomainBlockedAsync("recovering.example"));

        // First success: still blocked (needs 2 in a row).
        await service.RecordDeliveryOutcomeAsync("recovering.example", success: true);
        Assert.True(await service.IsDomainBlockedAsync("recovering.example"));

        // Second success: unblocked.
        var nowUnblocked = await service.RecordDeliveryOutcomeAsync("recovering.example", success: true);
        Assert.False(nowUnblocked);
        var peer = await repo.GetFederationPeerAsync("recovering.example");
        Assert.False(peer!.IsBlocked);
        Assert.Null(peer.BlockedAt);
    }

    [Fact]
    public async Task ConsecutiveProbeFailures_AutoBlockPeer()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions { AutoBlockProbeFailureThreshold = 2 });

        Assert.False(await service.RecordProbeOutcomeAsync("down.example", reachable: false));
        var blocked = await service.RecordProbeOutcomeAsync("down.example", reachable: false);

        Assert.True(blocked);
        var peer = await repo.GetFederationPeerAsync("down.example");
        Assert.True(peer!.IsBlocked);
        Assert.Equal(2, peer.ConsecutiveProbeFailures);
        Assert.NotNull(peer.BlockedReason);
    }

    [Fact]
    public async Task SuccessfulProbe_ResetsProbeFailureStreak()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions { AutoBlockProbeFailureThreshold = 2 });

        await service.RecordProbeOutcomeAsync("flappy.example", reachable: false);
        await service.RecordProbeOutcomeAsync("flappy.example", reachable: true); // reset
        await service.RecordProbeOutcomeAsync("flappy.example", reachable: false);

        Assert.False(await service.IsDomainBlockedAsync("flappy.example"));
        var peer = await repo.GetFederationPeerAsync("flappy.example");
        Assert.Equal(1, peer!.ConsecutiveProbeFailures);
    }

    [Fact]
    public async Task DisabledPeerHealth_RecordsButNeverBlocks()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions
        {
            Enabled = false,
            AutoBlockThreshold = 2
        });

        await service.RecordDeliveryOutcomeAsync("x.example", success: false);
        await service.RecordDeliveryOutcomeAsync("x.example", success: false);
        await service.RecordProbeOutcomeAsync("x.example", reachable: false);

        // Outcomes are still recorded (counters move) but no block occurs.
        var peer = await repo.GetFederationPeerAsync("x.example");
        Assert.Equal(2, peer!.ConsecutiveFailures);
        Assert.Equal(1, peer.ConsecutiveProbeFailures);
        Assert.False(peer.IsBlocked);
        Assert.False(await service.IsDomainBlockedAsync("x.example"));
    }

    [Fact]
    public async Task ManualBlockAndUnblock_Work()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions());

        await service.BlockDomainAsync("manual.example", reason: "abusive peer");
        Assert.True(await service.IsDomainBlockedAsync("manual.example"));
        var peer = await repo.GetFederationPeerAsync("manual.example");
        Assert.Equal("abusive peer", peer!.BlockedReason);

        await service.UnblockDomainAsync("manual.example");
        Assert.False(await service.IsDomainBlockedAsync("manual.example"));
        peer = await repo.GetFederationPeerAsync("manual.example");
        Assert.False(peer!.IsBlocked);
        Assert.Equal(0, peer.ConsecutiveFailures);
    }

    [Fact]
    public async Task GetBlockedDomainsAsync_ReturnsOnlyBlocked()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions { AutoBlockThreshold = 1 });

        await service.RecordDeliveryOutcomeAsync("bad.example", success: false);   // blocked
        await service.RecordDeliveryOutcomeAsync("good.example", success: true);  // not blocked

        var blocked = await service.GetBlockedDomainsAsync();
        Assert.Contains("bad.example", blocked);
        Assert.DoesNotContain("good.example", blocked);
    }

    [Fact]
    public async Task IsDomainBlockedAsync_UnknownDomain_ReturnsFalse()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions());

        Assert.False(await service.IsDomainBlockedAsync("never-seen.example"));
        Assert.False(await service.IsDomainBlockedAsync(""));
    }

    [Fact]
    public async Task GetPeersAsync_FiltersBlocked()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo, new PeerHealthOptions { AutoBlockThreshold = 1 });

        await service.RecordDeliveryOutcomeAsync("bad.example", success: false);
        await service.RecordDeliveryOutcomeAsync("good.example", success: true);

        var all = await service.GetPeersAsync(onlyBlocked: false);
        var blocked = await service.GetPeersAsync(onlyBlocked: true);

        Assert.Equal(2, all.Count);
        var single = Assert.Single(blocked);
        Assert.Equal("bad.example", single.Domain);
    }
}
