using ActivityPub.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace ActivityPub.Core.Caching;

/// <summary>
/// Default in-memory implementation of IFederationCache
/// Thread-safe implementation using IMemoryCache
/// </summary>
public class MemoryFederationCache : IFederationCache
{
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Index of cached keys and their scheduled expiry. This parallel index exists
    /// so domain/actor invalidation can find the keys it must evict without
    /// scanning the whole cache. It mirrors the <see cref="IMemoryCache"/> value
    /// store, so it must be pruned in lockstep: the underlying cache expires each
    /// entry by TTL, but without an equivalent here the index would accumulate
    /// every distinct key ever cached and grow unboundedly (a singleton leak).
    /// </summary>
    private readonly ConcurrentDictionary<string, KeyExpiry> _cacheKeys;

    // Cache TTL settings
    private static readonly TimeSpan ActorCacheTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ActivityCacheTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WebFingerCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InboxResponseCacheTtl = TimeSpan.FromMinutes(5);

    // Amortize the expired-key sweep across operations so per-call overhead is
    // negligible while still bounding index growth.
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);
    private DateTime _lastSweepUtc = DateTime.UtcNow;

    private sealed record KeyExpiry(DateTime ExpiresAtUtc);

    /// <summary>
    /// Creates a new MemoryFederationCache instance
    /// </summary>
    /// <param name="cache">The underlying IMemoryCache</param>
    public MemoryFederationCache(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheKeys = new ConcurrentDictionary<string, KeyExpiry>();
    }

    /// <summary>
    /// Records a key in the index with a scheduled expiry matching the value's
    /// TTL, so the index can be pruned in step with the cache.
    /// </summary>
    private void TrackKey(string key, TimeSpan ttl)
    {
        _cacheKeys[key] = new KeyExpiry(DateTime.UtcNow.Add(ttl));
        PruneExpired();
    }

    /// <summary>
    /// Removes an index entry (and the cache value) for a key.
    /// </summary>
    private void ForgetKey(string key)
    {
        _cacheKeys.TryRemove(key, out _);
        _cache.Remove(key);
    }

    /// <summary>
    /// Drops index entries whose values have already expired in the cache, so
    /// the index does not outlive the values it indexes. Called opportunistically
    /// at most once per <see cref="SweepInterval"/> from <see cref="TrackKey"/>,
    /// but also exposed so it can be driven from tests or a timer.
    /// </summary>
    public void PruneExpired(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (now - _lastSweepUtc < SweepInterval)
            return;

        var lastSweep = _lastSweepUtc;
        _lastSweepUtc = now;

        foreach (var kvp in _cacheKeys)
        {
            if (now >= kvp.Value.ExpiresAtUtc)
            {
                if (_cacheKeys.TryRemove(kvp.Key, out _))
                {
                    // Best-effort: the value has almost certainly already expired
                    // in the cache; removing it again is harmless.
                    _cache.Remove(kvp.Key);
                }
            }
        }
    }

    #region Actor Caching

    public async Task<Actor?> GetActorAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return null;

        if (_cache.TryGetValue(uri, out Actor? actor))
        {
            return actor;
        }

        return null;
    }

    public async Task SetActorAsync(string uri, Actor actor)
    {
        if (string.IsNullOrEmpty(uri) || actor == null)
            return;

        _cache.Set(uri, actor, ActorCacheTtl);
        TrackKey(uri, ActorCacheTtl);
    }

    public async Task RemoveActorAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return;

        ForgetKey(uri);
    }

    public async Task InvalidateActorsByDomainAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return;

        var keysToRemove = _cacheKeys.Keys
            .Where(key => key.Contains(domain, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            ForgetKey(key);
        }
    }

    #endregion

    #region Activity Caching

    public async Task<Activity?> GetActivityAsync(string activityId)
    {
        if (string.IsNullOrEmpty(activityId))
            return null;

        if (_cache.TryGetValue(activityId, out Activity? activity))
        {
            return activity;
        }

        return null;
    }

    public async Task SetActivityAsync(string activityId, Activity activity)
    {
        if (string.IsNullOrEmpty(activityId) || activity == null)
            return;

        _cache.Set(activityId, activity, ActivityCacheTtl);
        TrackKey(activityId, ActivityCacheTtl);
    }

    public async Task RemoveActivityAsync(string activityId)
    {
        if (string.IsNullOrEmpty(activityId))
            return;

        ForgetKey(activityId);
    }

    public async Task InvalidateActivitiesByActorAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            return;

        var keysToRemove = _cacheKeys.Keys
            .Where(key => key.Contains(actorId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            ForgetKey(key);
        }
    }

    #endregion

    #region WebFinger Caching

    public async Task<WebFingerResponse?> GetWebFingerResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (_cache.TryGetValue(key, out WebFingerResponse? response))
        {
            return response;
        }

        return null;
    }

    public async Task SetWebFingerResponseAsync(string key, WebFingerResponse response)
    {
        if (string.IsNullOrEmpty(key) || response == null)
            return;

        _cache.Set(key, response, WebFingerCacheTtl);
        TrackKey(key, WebFingerCacheTtl);
    }

    public async Task RemoveWebFingerResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        ForgetKey(key);
    }

    public async Task InvalidateWebFingerByDomainAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return;

        var keysToRemove = _cacheKeys.Keys
            .Where(key => key.Contains(domain, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            ForgetKey(key);
        }
    }

    #endregion

    #region Inbox Response Caching

    public async Task<string?> GetInboxResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (_cache.TryGetValue(key, out object? response) && response is string responseValue)
        {
            return responseValue;
        }

        return null;
    }

    public async Task SetInboxResponseAsync(string key, string response)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(response))
            return;

        _cache.Set(key, response, InboxResponseCacheTtl);
        TrackKey(key, InboxResponseCacheTtl);
    }

    public async Task RemoveInboxResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        ForgetKey(key);
    }

    public async Task InvalidateInboxResponsesByActorAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            return;

        var keysToRemove = _cacheKeys.Keys
            .Where(key => key.Contains(actorId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            ForgetKey(key);
        }
    }

    #endregion

    #region Cache Management

    public async Task ClearAsync()
    {
        foreach (var key in _cacheKeys.Keys.ToList())
        {
            _cache.Remove(key);
        }
        _cacheKeys.Clear();
    }

    public int Count
    {
        get
        {
            // Reading the count is a natural place to opportunistically drop
            // expired index entries, so the reported count tracks the live cache.
            PruneExpired();
            return _cacheKeys.Count;
        }
    }

    #endregion
}
