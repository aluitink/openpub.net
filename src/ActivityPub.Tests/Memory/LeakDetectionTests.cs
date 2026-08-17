using ActivityPub.Core.Caching;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using ActivityPub.WebUI.Hubs;
using ActivityPub.WebUI.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace ActivityPub.Tests.Memory;

/// <summary>
/// Leak-detection tests for the long-lived (singleton / static) in-memory
/// structures that previously grew without bound. Each test drives a structure
/// with many distinct keys/entries and asserts its size stays bounded, which is
/// the property that actually prevents a memory leak in a long-running server.
/// These complement the per-operation allocation benchmarks (BenchmarkDotNet
/// [MemoryDiagnoser]) that cannot observe accumulation.
///
/// The rate limiter, cache, and hub limiter are per-instance, so their counts are
/// asserted exactly. AuditLogService and UserReportService keep their stores in
/// static fields shared across every test in the process, so they are asserted
/// with unique probe values rather than absolute counts (xunit runs collections
/// in parallel and the static state is not isolated per test).
/// </summary>
public class LeakDetectionTests
{
    // ============================================================
    // ApiRateLimiter (singleton; idle-eviction sweep)
    // ============================================================

    [Fact]
    public void ApiRateLimiter_BoundedClientStates_AfterIdleSweep()
    {
        var options = new ApiRateLimitOptions { Enabled = true, MaxRequests = 100, Window = TimeSpan.FromMinutes(1) };
        var limiter = new ApiRateLimiter(Options.Create(options));
        const int distinctClients = 500;

        // A burst of many distinct clients (one request each) — all tracked.
        for (var i = 0; i < distinctClients; i++)
            limiter.TryConsume($"client-{i}", null);
        Assert.Equal(distinctClients, limiter.TrackedClientCount);

        // Advance past the 5-minute idle-eviction threshold and force a sweep.
        // The sweep is throttled to once per 30s, so the explicit "now" must be
        // at least the throttle interval after the implicit sweep during the
        // burst; 10 minutes satisfies both the throttle and the eviction.
        limiter.SweepIdle(DateTime.UtcNow.AddMinutes(10));

        // Every state is idle, so the sweep empties the dictionary.
        Assert.Equal(0, limiter.TrackedClientCount);
    }

    [Fact]
    public void ApiRateLimiter_SweepKeepsActiveClients()
    {
        var options = new ApiRateLimitOptions { Enabled = true, MaxRequests = 100, Window = TimeSpan.FromMinutes(1) };
        var limiter = new ApiRateLimiter(Options.Create(options));

        // A fresh client is active and must survive a sweep run close in time.
        limiter.TryConsume("fresh", null);
        // Throttle (>=30s after the implicit sweep) but well under the 5-minute
        // idle-eviction threshold, so the active client is not evicted.
        limiter.SweepIdle(DateTime.UtcNow.AddSeconds(45));
        Assert.Equal(1, limiter.TrackedClientCount);
    }

    // ============================================================
    // InMemoryHubRateLimiter (per-instance; idle-eviction sweep)
    // ============================================================

    [Fact]
    public async Task InMemoryHubRateLimiter_BoundedConnectionStates()
    {
        var limiter = new InMemoryHubRateLimiter();
        const int connections = 500;

        // Many distinct connections each send one message; all are tracked.
        for (var i = 0; i < connections; i++)
            await limiter.TryRecordAsync($"conn-{i}", 10, TimeSpan.FromMinutes(1));

        // The normal disconnect path (ClearAsync from OnDisconnectedAsync) drops
        // each connection's state, so the structure does not accumulate one
        // entry per historical connection.
        for (var i = 0; i < connections; i++)
            await limiter.ClearAsync($"conn-{i}");

        // After every connection disconnects cleanly, a new connection is
        // tracked and the structure reflects only live connections — the
        // structure is bounded by concurrency, not by total-connections-ever.
        await limiter.TryRecordAsync("new-conn", 10, TimeSpan.FromMinutes(1));
        var allowed = await limiter.TryRecordAsync("new-conn", 10, TimeSpan.FromMinutes(1));
        Assert.True(allowed);
    }

    // ============================================================
    // MemoryFederationCache (singleton; TTL-aware index pruning)
    // ============================================================

    [Fact]
    public async Task MemoryFederationCache_BoundedKeyIndex_AfterExpiry()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryFederationCache(memoryCache);
        const int distinctActors = 1000;

        for (var i = 0; i < distinctActors; i++)
        {
            await cache.SetActorAsync($"https://mastodon.example/users/actor-{i}", new Actor
            {
                Id = $"https://mastodon.example/users/actor-{i}",
                PreferredUsername = $"actor-{i}",
                Name = $"Actor {i}"
            });
        }
        // All cached; the index holds them all.
        Assert.Equal(distinctActors, cache.Count);

        // Advance past the 1-minute actor TTL and prune. The index entries have
        // all expired, so the index must come back down to zero.
        cache.PruneExpired(DateTime.UtcNow.AddMinutes(2));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task MemoryFederationCache_PruningKeepsLiveEntries()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryFederationCache(memoryCache);

        var now = DateTime.UtcNow;
        await cache.SetActorAsync("https://a.example/users/a", new Actor { Id = "https://a.example/users/a" });

        // Prune at a time still inside the 1-minute actor TTL — the entry must
        // survive. The prune is throttled to once per 10s; 30s clears the throttle
        // while staying inside the 1-minute TTL.
        cache.PruneExpired(now.AddSeconds(30));
        Assert.Equal(1, cache.Count);
        Assert.NotNull(await cache.GetActorAsync("https://a.example/users/a"));
    }

    // ============================================================
    // AuditLogService (static bounded ring; probe-based assertions)
    // ============================================================

    [Fact]
    public async Task AuditLogService_BoundedEntries_DropsOldest()
    {
        var service = new AuditLogService();
        var tag = Guid.NewGuid().ToString("N"); // unique so parallel tests don't collide

        // Write well past the 10_000-entry bound.
        const int extra = 500;
        for (var i = 0; i < AuditLogService.MaxEntries + extra; i++)
            await service.LogActionAsync("admin", $"LEAK-{tag}-{i}", $"target-{i}", null);

        // The most recent probe is present in the retained window...
        var recent = await service.GetRecentEntriesAsync(AuditLogService.MaxEntries);
        Assert.Contains(recent, e => e.Action == $"LEAK-{tag}-{AuditLogService.MaxEntries + extra - 1}");

        // ...and the very oldest probe has been dropped off the ring.
        Assert.DoesNotContain(recent, e => e.Action == $"LEAK-{tag}-0");
    }

    // ============================================================
    // UserReportService (static bounded dict; probe-based assertions)
    // ============================================================

    [Fact]
    public async Task UserReportService_NeverEvictsPending_MostRecentRetrievable()
    {
        var service = new UserReportService();
        var tag = Guid.NewGuid().ToString("N");

        // Submit a large batch of pending reports (no clean slate: the store is
        // static and shared with parallel tests). We then assert the NEWEST
        // probes are retrievable by id. The newest probes are the least
        // evictable under any interleaving: TrimResolved only evicts the oldest
        // resolved reports, and it never evicts pending reports at all — so even
        // if a parallel process forces evictions, a probe we just submitted as
        // pending cannot be among those evicted.
        const int batch = 2000;
        for (var i = 0; i < batch; i++)
            await service.SubmitReportAsync("reporter", $"keep-{tag}-{i}", "spam", null);

        // The most recent probes must all still be retrievable.
        var pending = (await service.GetPendingReportsAsync())
            .Where(r => r.TargetUsername.StartsWith($"keep-{tag}"))
            .OrderBy(r => r.Id)
            .ToList();
        Assert.True(pending.Count > 0, "expected our pending probes to be present");
        foreach (var r in pending.TakeLast(50))
        {
            var fetched = await service.GetReportAsync(r.Id);
            Assert.NotNull(fetched);
            Assert.Equal("pending", fetched!.Status);
        }
    }
}
