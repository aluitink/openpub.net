using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;
using ActivityPub.Core.Infrastructure;
using ActivityPub.Core.Options;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ActivityPub.Core;

/// <summary>
/// WebFinger endpoint implementation for ActivityPub protocol
/// </summary>
[ApiController]
[Route(".well-known/webfinger")]
public class WebFingerController : ControllerBase
{
    private readonly WebFingerCacheService _cacheService;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ActivityPubOptions _options;

    public WebFingerController(WebFingerCacheService cacheService, IOptions<ActivityPubOptions> options)
    {
        _cacheService = cacheService;
        _jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new WebFingerJsonConverter() }
        };
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetWebFinger(
        [FromQuery] string? resource,
        [FromQuery] string? rel)
    {
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
        
        // Return serialized JRD with proper content type
        var jsonResponse = JsonSerializer.Serialize(jrd, _jsonOptions);
        return Content(jsonResponse, "application/jrd+json");
    }

    private string GetActivityPubEndpoint(string resource)
    {
        if (resource.StartsWith("acct:"))
        {
            var accountInfo = resource.Substring(5);
            var parts = accountInfo.Split('@');
            if (parts.Length >= 2)
            {
                var username = parts[0];
                return $"{_options.Domain}{_options.UserPath}/{username}";
            }
        }
        
        return resource;
    }
    

}