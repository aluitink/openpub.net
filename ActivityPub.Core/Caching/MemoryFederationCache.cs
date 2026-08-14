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
    private readonly ConcurrentDictionary<string, bool> _cacheKeys;

    // Cache TTL settings
    private static readonly TimeSpan ActorCacheTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ActivityCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan WebFingerCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InboxResponseCacheTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Creates a new MemoryFederationCache instance
    /// </summary>
    /// <param name="cache">The underlying IMemoryCache</param>
    public MemoryFederationCache(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheKeys = new ConcurrentDictionary<string, bool>();
    }

    #region Actor Caching

    public async Task<Actor?> GetActorAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return null;

        if (_cache.TryGetValue(uri, out Actor actor))
        {
            return actor;
        }

        return null;
    }

    public async Task SetActorAsync(string uri, Actor actor)
    {
        if (string.IsNullOrEmpty(uri) || actor == null)
            return;

        _cacheKeys.TryAdd(uri, true);
        _cache.Set(uri, actor, ActorCacheTtl);
    }

    public async Task RemoveActorAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return;

        _cacheKeys.TryRemove(uri, out _);
        _cache.Remove(uri);
    }

    public async Task InvalidateActorsByDomainAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return;

        var keysToRemove = new List<string>();

        foreach (var key in _cacheKeys.Keys)
        {
            if (key.StartsWith($"actor:{domain}", StringComparison.OrdinalIgnoreCase))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _cacheKeys.TryRemove(key, out _);
            _cache.Remove(key);
        }
    }

    #endregion

    #region Activity Caching

    public async Task<Activity?> GetActivityAsync(string activityId)
    {
        if (string.IsNullOrEmpty(activityId))
            return null;

        if (_cache.TryGetValue(activityId, out Activity activity))
        {
            return activity;
        }

        return null;
    }

    public async Task SetActivityAsync(string activityId, Activity activity)
    {
        if (string.IsNullOrEmpty(activityId) || activity == null)
            return;

        _cacheKeys.TryAdd(activityId, true);
        _cache.Set(activityId, activity, ActivityCacheTtl);
    }

    public async Task RemoveActivityAsync(string activityId)
    {
        if (string.IsNullOrEmpty(activityId))
            return;

        _cacheKeys.TryRemove(activityId, out _);
        _cache.Remove(activityId);
    }

    public async Task InvalidateActivitiesByActorAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            return;

        var keysToRemove = new List<string>();

        foreach (var key in _cacheKeys.Keys)
        {
            if (key.Contains(actorId, StringComparison.OrdinalIgnoreCase))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _cacheKeys.TryRemove(key, out _);
            _cache.Remove(key);
        }
    }

    #endregion

    #region WebFinger Caching

    public async Task<WebFingerResponse?> GetWebFingerResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (_cache.TryGetValue(key, out WebFingerResponse response))
        {
            return response;
        }

        return null;
    }

    public async Task SetWebFingerResponseAsync(string key, WebFingerResponse response)
    {
        if (string.IsNullOrEmpty(key) || response == null)
            return;

        _cacheKeys.TryAdd(key, true);
        _cache.Set(key, response, WebFingerCacheTtl);
    }

    public async Task RemoveWebFingerResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        _cacheKeys.TryRemove(key, out _);
        _cache.Remove(key);
    }

    public async Task InvalidateWebFingerByDomainAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return;

        var keysToRemove = new List<string>();

        foreach (var key in _cacheKeys.Keys)
        {
            if (key.Contains(domain, StringComparison.OrdinalIgnoreCase))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _cacheKeys.TryRemove(key, out _);
            _cache.Remove(key);
        }
    }

    #endregion

    #region Inbox Response Caching

    public async Task<string?> GetInboxResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (_cache.TryGetValue(key, out string response))
        {
            return response;
        }

        return null;
    }

    public async Task SetInboxResponseAsync(string key, string response)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(response))
            return;

        _cacheKeys.TryAdd(key, true);
        _cache.Set(key, response, InboxResponseCacheTtl);
    }

    public async Task RemoveInboxResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        _cacheKeys.TryRemove(key, out _);
        _cache.Remove(key);
    }

    public async Task InvalidateInboxResponsesByActorAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            return;

        var keysToRemove = new List<string>();

        foreach (var key in _cacheKeys.Keys)
        {
            if (key.Contains(actorId, StringComparison.OrdinalIgnoreCase))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _cacheKeys.TryRemove(key, out _);
            _cache.Remove(key);
        }
    }

    #endregion

    #region Cache Management

    public async Task ClearAsync()
    {
        foreach (var key in _cacheKeys.Keys)
        {
            _cache.Remove(key);
        }
        _cacheKeys.Clear();
    }

    public int Count
    {
        get
        {
            return _cacheKeys.Count;
        }
    }

    #endregion
}
