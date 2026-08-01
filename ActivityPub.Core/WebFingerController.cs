using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core.Services;
using ActivityPub.Core.Infrastructure.Telemetry;
using ActivityPub.Core.Models;
using ActivityPub.Core.Infrastructure;
using System.Text.Json;
using System.Diagnostics;

namespace ActivityPub.Core;

/// <summary>
/// WebFinger endpoint implementation for ActivityPub protocol
/// </summary>
[ApiController]
[Route(".well-known/[controller]")]
public class WebFingerController : ControllerBase
{
    private readonly WebFingerCacheService _cacheService;
    private readonly ActivityPubTelemetry _telemetry;
    private readonly JsonSerializerOptions _jsonOptions;

    public WebFingerController(WebFingerCacheService cacheService, ActivityPubTelemetry telemetry)
    {
        _cacheService = cacheService;
        _telemetry = telemetry;
        _jsonOptions = new JsonSerializerOptions
        {
            Converters = { new WebFingerJsonConverter() }
        };
    }

    [HttpGet("webfinger")]
    public async Task<IActionResult> GetWebFinger(
        [FromQuery] string? resource,
        [FromQuery] string? rel)
    {
        // Record the WebFinger request
        _telemetry.RecordWebFingerRequest();
        
        // Validate required parameters according to W3C WebFinger specification
        if (string.IsNullOrEmpty(resource))
        {
            return BadRequest(new { error = "resource parameter is required" });
        }

        // Generate cache key based on resource and rel parameters
        var cacheKey = $"{resource}:{rel}";
        
        // Try to get from cache
        var stopwatch = Stopwatch.StartNew();
        var cachedResponse = _cacheService.GetCachedResponse(cacheKey);
        
        if (cachedResponse != null)
        {
            stopwatch.Stop();
            _telemetry.RecordWebFingerProcessingTime(stopwatch.ElapsedMilliseconds);
            
            // Return cached response with proper JSON serialization
            var json = JsonSerializer.Serialize(cachedResponse, _jsonOptions);
            return Content(json, "application/jrd+json");
        }
        
        // Handle the resource according to WebFinger specification
        var subject = resource;
        var links = new List<WebFingerLink>();
        
        // Add self link to the ActivityPub endpoint
        var activityPubEndpoint = GetActivityPubEndpoint(resource);
        links.Add(new WebFingerLink
        {
            Rel = "self",
            Type = "application/activity+json",
            Href = activityPubEndpoint
        });
        
        // Add additional links if rel parameter is provided
        if (!string.IsNullOrEmpty(rel))
        {
            // This is a simplified implementation
        }
        
        // Create JRD response
        var jrd = new WebFingerJrd
        {
            Subject = subject,
            Links = links.ToList()
        };
        
        // Cache the response - ensure type compatibility
        var cachedLinks = new List<WebFingerLink>();
        foreach (var link in jrd.Links)
        {
            cachedLinks.Add(new WebFingerLink
            {
                Rel = link.Rel,
                Type = link.Type,
                Href = link.Href
            });
        }
        
        _cacheService.SetCachedResponse(cacheKey, new WebFingerResponse
        {
            Subject = jrd.Subject,
            Links = cachedLinks.ToArray(),
            CachedAt = DateTime.UtcNow
        });
        
        stopwatch.Stop();
        _telemetry.RecordWebFingerProcessingTime(stopwatch.ElapsedMilliseconds);
        
        // Return serialized JRD with proper content type
        var jsonResponse = JsonSerializer.Serialize(jrd, _jsonOptions);
        return Content(jsonResponse, "application/jrd+json");
    }

    private string GetActivityPubEndpoint(string resource)
    {
        // According to W3C WebFinger specification for ActivityPub:
        // The resource URI should resolve to an ActivityPub actor
        // We'll handle common formats like acct: usernames
        
        if (resource.StartsWith("acct:"))
        {
            // Extract username from acct:username@domain format
            var accountInfo = resource.Substring(5); // Remove "acct:" prefix
            var parts = accountInfo.Split('@');
            if (parts.Length >= 2)
            {
                var username = parts[0];
                var domain = parts[1];
                // Return standard ActivityPub user endpoint
                return $"/users/{username}";
            }
        }
        
        // For other resource types, return as-is or construct appropriate endpoint
        return resource;
    }
    
    [HttpGet("cache-stats")]
    public IActionResult GetCacheStats()
    {
        // Create comprehensive cache statistics for monitoring and debugging
        var stats = new WebFingerCacheStats
        {
            Timestamp = DateTime.UtcNow,
            CacheSize = 0, // Due to limited access to internal cache state, we'll use telemetry
            CacheHits = _telemetry.GetWebFingerCacheHits(),
            CacheMisses = _telemetry.GetWebFingerCacheMisses(),
            HitRatio = _telemetry.GetWebFingerCacheHitRatio(),
            MissRatio = 1.0 - _telemetry.GetWebFingerCacheHitRatio(), // Calculate miss ratio
            TotalRequests = _telemetry.GetWebFingerRequests(),
            CacheLifetime = "10 minutes",
            CacheType = "MemoryCache",
            CacheImplementationDetails = "Cache statistics exposed via ActivityPub telemetry"
        };

        return Ok(stats);
    }
}