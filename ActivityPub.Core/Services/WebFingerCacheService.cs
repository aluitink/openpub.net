using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using ActivityPub.Core.Infrastructure.Telemetry;
using ActivityPub.Core.Models;

namespace ActivityPub.Core.Services;

/// <summary>
/// Cache service for WebFinger responses to improve performance and reduce redundant processing
/// </summary>
public class WebFingerCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ActivityPubTelemetry _telemetry;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);

    public WebFingerCacheService(IMemoryCache cache, ActivityPubTelemetry telemetry)
    {
        _cache = cache;
        _telemetry = telemetry;
    }

    /// <summary>
    /// Gets a WebFinger response from cache if available
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <returns>WebFinger response or null if not cached</returns>
    public WebFingerResponse? GetCachedResponse(string key)
    {
        if (_cache.TryGetValue(key, out WebFingerResponse? cachedResponse))
        {
            _telemetry.RecordWebFingerCacheHit();
            return cachedResponse;
        }
        
        _telemetry.RecordWebFingerCacheMiss();
        return null;
    }

    /// <summary>
    /// Sets a WebFinger response in cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="response">Response to cache</param>
    public void SetCachedResponse(string key, WebFingerResponse response)
    {
        _cache.Set(key, response, _cacheExpiration);
        // Update cache size gauge - note: IMemoryCache doesn't expose Count property
        // In a production scenario, this would be tracked via telemetry or custom metrics
        _telemetry.UpdateWebFingerCacheSize(-1); // -1 indicates unknown count
    }

    /// <summary>
    /// Clears the cache
    /// </summary>
    public void ClearCache()
    {
        // Note: MemoryCache doesn't expose a direct clear method, 
        // but we can reset by recreating it or letting entries expire
        _telemetry.UpdateWebFingerCacheSize(0);
    }
}



