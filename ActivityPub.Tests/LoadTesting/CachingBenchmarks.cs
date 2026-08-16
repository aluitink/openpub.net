using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Memory;
using ActivityPub.Core.Caching;
using ActivityPub.Core.Models;

namespace ActivityPub.Tests.LoadTesting;

/// <summary>
/// Benchmarks federation caching operations using MemoryFederationCache backed by IMemoryCache.
/// Measures cache hit/miss, set, invalidation, and bulk clear operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class CachingBenchmarks
{
    private MemoryFederationCache? _cache;
    private Actor? _actor;
    private Activity? _activity;
    private WebFingerResponse? _webFingerResponse;
    private string? _inboxResponse;

    // Pre-generated cache keys for benchmarking
    private readonly string[] _actorKeys = new string[100];
    private readonly string[] _activityKeys = new string[100];
    private readonly string[] _webFingerKeys = new string[100];
    private readonly string[] _inboxKeys = new string[100];

    [GlobalSetup]
    public void GlobalSetup()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new MemoryFederationCache(memoryCache);

        // Prepare test data
        _actor = new Actor
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/alice",
            Type = "Person",
            Name = "Alice Example",
            PreferredUsername = "alice",
            Url = "https://example.com/users/alice",
            Inbox = "https://example.com/users/alice/inbox",
            Outbox = "https://example.com/users/alice/outbox",
            PublicKey = new PublicKey
            {
                Id = "https://example.com/users/alice#main-key",
                Owner = "https://example.com/users/alice",
                PublicKeyPem = "-----BEGIN RSA PUBLIC KEY-----\nMIIBCgKCAQEA2a2rwplBQLHgCL3M3i8pM3UcH8MiU9D5jcb4OCFe0pE\n-----END RSA PUBLIC KEY-----",
            },
        };

        _activity = new Activity
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = "https://example.com/users/alice/activities/12345",
            Type = "Create",
            Actor = "https://example.com/users/alice",
            Object = "https://example.com/notes/12345",
            Published = DateTime.UtcNow,
        };

        _webFingerResponse = new WebFingerResponse
        {
            Subject = "acct:alice@example.com",
            Links = new[]
            {
                new WebFingerLink { Rel = "self", Type = "application/activity+json", Href = "https://example.com/users/alice" },
            },
            CachedAt = DateTime.UtcNow,
        };

        _inboxResponse = "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"type\":\"OrderedCollection\",\"totalItems\":0,\"orderedItems\":[]}";

        // Generate cache keys
        for (int i = 0; i < 100; i++)
        {
            _actorKeys[i] = $"https://example.com/users/user{i}";
            _activityKeys[i] = $"https://example.com/users/user{i}/activities/act{i}";
            _webFingerKeys[i] = $"acct:user{i}@example.com";
            _inboxKeys[i] = $"inbox:user{i}@example.com:timestamp";
        }
    }

    // ===== Actor Cache Operations =====

    [Benchmark(Baseline = true)]
    public async Task SetActor()
    {
        await _cache!.SetActorAsync("https://example.com/users/alice", _actor!);
    }

    [Benchmark]
    public async Task GetActor_Hit()
    {
        await _cache!.SetActorAsync("https://example.com/users/alice", _actor!);
        await _cache.GetActorAsync("https://example.com/users/alice");
    }

    [Benchmark]
    public async Task GetActor_Miss()
    {
        await _cache!.GetActorAsync("https://example.com/users/nonexistent-" + _actorKeys[0]);
    }

    [Benchmark]
    public async Task RemoveActor()
    {
        await _cache!.SetActorAsync("https://example.com/users/alice", _actor!);
        await _cache.RemoveActorAsync("https://example.com/users/alice");
    }

    // ===== Activity Cache Operations =====

    [Benchmark]
    public async Task SetActivity()
    {
        await _cache!.SetActivityAsync("https://example.com/users/alice/activities/12345", _activity!);
    }

    [Benchmark]
    public async Task GetActivity_Hit()
    {
        await _cache!.SetActivityAsync("https://example.com/users/alice/activities/12345", _activity!);
        await _cache.GetActivityAsync("https://example.com/users/alice/activities/12345");
    }

    [Benchmark]
    public async Task GetActivity_Miss()
    {
        await _cache!.GetActivityAsync("https://example.com/users/nonexistent/activities/miss");
    }

    // ===== WebFinger Cache Operations =====

    [Benchmark]
    public async Task SetWebFingerResponse()
    {
        await _cache!.SetWebFingerResponseAsync("acct:alice@example.com", _webFingerResponse!);
    }

    [Benchmark]
    public async Task GetWebFingerResponse_Hit()
    {
        await _cache!.SetWebFingerResponseAsync("acct:alice@example.com", _webFingerResponse!);
        await _cache.GetWebFingerResponseAsync("acct:alice@example.com");
    }

    [Benchmark]
    public async Task GetWebFingerResponse_Miss()
    {
        await _cache!.GetWebFingerResponseAsync("acct:nonexistent@example.com");
    }

    // ===== Inbox Response Cache Operations =====

    [Benchmark]
    public async Task SetInboxResponse()
    {
        await _cache!.SetInboxResponseAsync("inbox:user@domain", _inboxResponse!);
    }

    [Benchmark]
    public async Task GetInboxResponse_Hit()
    {
        await _cache!.SetInboxResponseAsync("inbox:user@domain", _inboxResponse!);
        await _cache.GetInboxResponseAsync("inbox:user@domain");
    }

    // ===== Bulk / Invalidation Operations =====

    [Benchmark]
    public async Task Set100Actors()
    {
        for (int i = 0; i < 100; i++)
        {
            var variantActor = new Actor
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Id = _actorKeys[i],
                Type = "Person",
                Name = $"User {i}",
                PreferredUsername = $"user{i}",
                Url = _actorKeys[i],
                Inbox = $"{_actorKeys[i]}/inbox",
                Outbox = $"{_actorKeys[i]}/outbox",
            };
            await _cache!.SetActorAsync(_actorKeys[i], variantActor);
        }
    }

    [Benchmark]
    public async Task InvalidateActorsByDomain()
    {
        // Pre-populate
        for (int i = 0; i < 100; i++)
        {
            var variantActor = new Actor
            {
                Id = _actorKeys[i],
                Type = "Person",
                Name = $"User {i}",
            };
            await _cache!.SetActorAsync(_actorKeys[i], variantActor);
        }
        // Now benchmark the invalidation
        await _cache.InvalidateActorsByDomainAsync("example.com");
    }

    [Benchmark]
    public async Task ClearCache()
    {
        // Pre-populate
        for (int i = 0; i < 100; i++)
        {
            var variantActor = new Actor { Id = _actorKeys[i], Type = "Person", Name = $"User {i}" };
            await _cache!.SetActorAsync(_actorKeys[i], variantActor);
        }
        await _cache.SetActivityAsync(_activityKeys[0], _activity!);
        await _cache.SetWebFingerResponseAsync(_webFingerKeys[0], _webFingerResponse!);
        await _cache.SetInboxResponseAsync(_inboxKeys[0], _inboxResponse!);

        // Benchmark clear
        await _cache.ClearAsync();
    }
}
