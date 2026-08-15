using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents detailed cache statistics for WebFinger operations
/// </summary>
public class WebFingerCacheStats
{
    /// <summary>
    /// Timestamp when statistics were collected
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Current number of items in the cache
    /// </summary>
    public int CacheSize { get; set; }

    /// <summary>
    /// Total number of cache hits
    /// </summary>
    public long CacheHits { get; set; }

    /// <summary>
    /// Total number of cache misses
    /// </summary>
    public long CacheMisses { get; set; }

    /// <summary>
    /// Cache hit ratio (0.0 to 1.0)
    /// </summary>
    public double HitRatio { get; set; }

    /// <summary>
    /// Cache miss ratio (0.0 to 1.0)
    /// </summary>
    public double MissRatio { get; set; }

    /// <summary>
    /// Total number of WebFinger requests processed
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Time the cache was configured to expire items
    /// </summary>
    public string CacheLifetime { get; set; } = "10 minutes";

    /// <summary>
    /// Type of cache implementation used
    /// </summary>
    public string CacheType { get; set; } = "MemoryCache";

    /// <summary>
    /// Cache implementation details
    /// </summary>
    public string CacheImplementationDetails { get; set; } = "Cache statistics exposed via ActivityPub telemetry";
}