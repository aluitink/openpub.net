using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
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
        _cache = cache;
    }

    public WebFingerResponse? GetCachedResponse(string key)
    {
        if (_cache.TryGetValue(key, out WebFingerResponse? cachedResponse))
        {
            return cachedResponse;
        }
        
        return null;
    }

    public void SetCachedResponse(string key, WebFingerResponse response)
    {
        _cache.Set(key, response, _cacheExpiration);
    }

    public void ClearCache()
    {
    }
}
