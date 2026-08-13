using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActivityPub.Core.Controllers.Versioned;

/// <summary>
/// Versioned WebFinger controller to demonstrate API versioning
/// </summary>
[ApiController]
    [Route("api/v{version:apiVersion}/webfinger")]
public class WebFingerVersionedController : ControllerBase
{
    private readonly WebFingerCacheService _cacheService;
    private readonly JsonSerializerOptions _jsonOptions;

    public WebFingerVersionedController(WebFingerCacheService cacheService)
    {
        _cacheService = cacheService;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new ActivityPub.Core.Infrastructure.WebFingerJsonConverter() }
        };
    }

    /// <summary>
    /// Gets WebFinger resource descriptor with versioning support
    /// </summary>
    /// <param name="resource">The resource identifier in acct: format</param>
    /// <returns>JSON Resource Descriptor (JRD) response</returns>
    [HttpGet]
    public async Task<IActionResult> GetWebFinger(
        [FromQuery(Name = "resource")] string? resource,
        [FromQuery(Name = "rel")] string? rel)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            var json = JsonSerializer.Serialize(new { error = "Missing required resource parameter" });
            var result = Content(json, "application/json");
            result.StatusCode = 400;
            return result;
        }

        var cacheKey = $"{resource}:{rel}";
        var cachedResponse = _cacheService.GetCachedResponse(cacheKey);

        if (cachedResponse != null)
        {
            var json = JsonSerializer.Serialize(cachedResponse, _jsonOptions);
            return Content(json, "application/jrd+json");
        }

        var subject = resource;
        var links = new List<WebFingerLink>();

        var activityPubEndpoint = GetActivityPubEndpoint(resource);
        links.Add(new WebFingerLink
        {
            Rel = "self",
            Type = "application/activity+json",
            Href = activityPubEndpoint
        });

        var jrd = new WebFingerJrd
        {
            Subject = subject,
            Links = links.ToList()
        };

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
                return $"/users/{username}";
            }
        }

        return resource;
    }
}