using System.Diagnostics.Metrics;
using ActivityPub.Core.Caching;
using ActivityPub.Core.Events;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Infrastructure.Telemetry;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Implementations;

/// <summary>
/// Unit tests for <see cref="DefaultWebFingerSource"/> — the WebFinger
/// resource resolver, which previously had no unit test. Covers acct:
/// parsing, the cache-first lookup, the repository actor resolution +
/// cache-population, and the not-found / non-acct paths.
/// </summary>
public class DefaultWebFingerSourceTests
{
    private static readonly Actor Alice = new()
    {
        Id = "https://openpub.luit.ink/users/alice",
        Type = "Person",
        PreferredUsername = "alice"
    };

    private sealed record Harness(
        DefaultWebFingerSource Source,
        Mock<IActivityPubRepository> Repo,
        WebFingerCacheService Cache)
    {
        public static Harness Create(Func<string, Actor?>? actorLookup = null)
        {
            var repo = new Mock<IActivityPubRepository>();
            repo.Setup(r => r.GetUserActorAsync(It.IsAny<string>()))
                .ReturnsAsync((string username) => actorLookup?.Invoke(username) ?? null);

            var cacheService = new WebFingerCacheService(new MemoryCache(new MemoryCacheOptions()));
            var activityPubService = new ActivityPubService(
                repo.Object,
                new ActivityPub.Core.Services.ActivityPubEventDispatcher(),
                Array.Empty<IActivityPubInterceptor>(),
                NullLogger<ActivityPubService>.Instance,
                new Mock<IFederationCache>().Object,
                new CacheInvalidationService(new Mock<IFederationCache>().Object, NullLogger<CacheInvalidationService>.Instance));
            var telemetry = new ActivityPubTelemetry(NullLogger<ActivityPubTelemetry>.Instance, new Meter("test"));
            var source = new DefaultWebFingerSource(
                repo.Object,
                activityPubService,
                cacheService,
                NullLogger<DefaultWebFingerSource>.Instance,
                telemetry);
            return new Harness(source, repo, cacheService);
        }
    }

    [Fact]
    public async Task AcctResource_ResolvesActor_ReturnsActorId_AndPopulatesCache()
    {
        var harness = Harness.Create(username => username == "alice" ? Alice : null);

        var result = await harness.Source.GetWebFingerResourceAsync("acct:alice@openpub.luit.ink");

        Assert.Equal("https://openpub.luit.ink/users/alice", result);

        // The resolution was cached for the resource key with a self link.
        var cached = harness.Cache.GetCachedResponse("webfinger:acct:alice@openpub.luit.ink");
        Assert.NotNull(cached);
        var self = Assert.Single(cached!.Links, l => l.Rel == "self");
        Assert.Equal("https://openpub.luit.ink/users/alice", self.Href);
    }

    [Fact]
    public async Task AcctResource_CacheHit_ReturnsCachedHref_WithoutRepositoryCall()
    {
        var harness = Harness.Create(username => throw new InvalidOperationException("should not be called"));

        // Pre-seed the cache as a prior resolution would have.
        harness.Cache.SetCachedResponse("webfinger:acct:bob@openpub.luit.ink",
            new WebFingerResponse
            {
                Subject = "acct:bob@openpub.luit.ink",
                Links = new[] { new WebFingerLink { Rel = "self", Href = "https://openpub.luit.ink/users/bob" } }
            });

        var result = await harness.Source.GetWebFingerResourceAsync("acct:bob@openpub.luit.ink");

        // Served from cache, not the repository (which would throw).
        Assert.Equal("https://openpub.luit.ink/users/bob", result);
    }

    [Fact]
    public async Task AcctResource_UnknownUser_ReturnsNull()
    {
        var harness = Harness.Create(_ => null);

        var result = await harness.Source.GetWebFingerResourceAsync("acct:nobody@openpub.luit.ink");

        Assert.Null(result);
    }

    [Fact]
    public async Task NonAcctResource_ReturnsNull()
    {
        var harness = Harness.Create(_ => Alice);

        // A non-acct: resource is not resolved (returns null).
        Assert.Null(await harness.Source.GetWebFingerResourceAsync("https://openpub.luit.ink/users/alice"));
        Assert.Null(await harness.Source.GetWebFingerResourceAsync("alice@openpub.luit.ink"));
    }

    [Fact]
    public async Task AcctResource_WithoutDomain_ReturnsNull()
    {
        var harness = Harness.Create(_ => Alice);

        // "acct:alice" has no '@domain' → parts.Length < 2 → null.
        Assert.Null(await harness.Source.GetWebFingerResourceAsync("acct:alice"));
    }

    [Fact]
    public async Task AcctResource_SecondResolution_IsServedFromCache()
    {
        var calls = 0;
        var harness = Harness.Create(username =>
        {
            calls++;
            return username == "alice" ? Alice : null;
        });

        var first = await harness.Source.GetWebFingerResourceAsync("acct:alice@openpub.luit.ink");
        var second = await harness.Source.GetWebFingerResourceAsync("acct:alice@openpub.luit.ink");

        Assert.Equal("https://openpub.luit.ink/users/alice", first);
        Assert.Equal("https://openpub.luit.ink/users/alice", second);
        // The repository was hit once (the second call is a cache hit).
        Assert.Equal(1, calls);
    }
}
