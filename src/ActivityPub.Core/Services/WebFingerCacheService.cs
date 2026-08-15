using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using ActivityPub.Core.Caching;
using ActivityPub.Core.Models;

namespace ActivityPub.Core.Services;

/// <summary>
/// Cache service for WebFinger responses to improve performance and reduce redundant processing
/// </summary>
public class WebFingerCacheService
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);

    public WebFingerCacheService(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public WebFingerResponse? GetCachedResponse(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (_cache.TryGetValue(key, out WebFingerResponse? cachedResponse))
        {
            return cachedResponse;
        }
        
        return null;
    }

    public void SetCachedResponse(string key, WebFingerResponse response)
    {
        if (string.IsNullOrEmpty(key) || response == null)
            return;

        _cache.Set(key, response, _cacheExpiration);
    }

    public void RemoveCachedResponse(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        _cache.Remove(key);
    }

    public async Task<WebFingerResponse?> GetCachedResponseAsync(string key)
    {
        return GetCachedResponse(key);
    }

    public async Task SetCachedResponseAsync(string key, WebFingerResponse response)
    {
        SetCachedResponse(key, response);
    }

    public async Task RemoveCachedResponseAsync(string key)
    {
        RemoveCachedResponse(key);
    }
}
