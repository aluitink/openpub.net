using ActivityPub.Core.Caching;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using ActivityPub.Core.Infrastructure.Telemetry;

namespace ActivityPub.Core.Implementations;

/// <summary>
/// Default implementation of IWebFingerSource for resolving ActivityPub actors
/// </summary>
public class DefaultWebFingerSource : IWebFingerSource
{
    private readonly IActivityPubRepository _repository;
    private readonly ActivityPubService _activityPubService;
    private readonly WebFingerCacheService _webFingerCache;
    private readonly ILogger<DefaultWebFingerSource> _logger;
    private readonly ActivityPubTelemetry _telemetry;

    public DefaultWebFingerSource(
        IActivityPubRepository repository,
        ActivityPubService activityPubService,
        WebFingerCacheService webFingerCache,
        ILogger<DefaultWebFingerSource> logger,
        ActivityPubTelemetry telemetry)
    {
        _repository = repository;
        _activityPubService = activityPubService;
        _webFingerCache = webFingerCache;
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task<string?> GetWebFingerResourceAsync(string resource)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Resolving WebFinger resource: {Resource}", resource);
        
        try
        {
            // Handle acct:username@domain format
            if (resource.StartsWith("acct:"))
            {
                var accountInfo = resource.Substring(5); // Remove "acct:" prefix
                var parts = accountInfo.Split('@');
                if (parts.Length >= 2)
                {
                    var username = parts[0];
                    var domain = parts[1];
                    var cacheKey = $"webfinger:{resource}";
                    
                    // Try cache first
                    var cachedResponse = await _webFingerCache.GetCachedResponseAsync(cacheKey);
                    if (cachedResponse != null && cachedResponse.Links.Length > 0)
                    {
                        _logger.LogInformation("WebFinger cache hit for resource: {Resource}", resource);
                        if (cachedResponse.Links.Any(l => l.Rel == "self"))
                        {
                            return cachedResponse.Links.First(l => l.Rel == "self").Href;
                        }
                    }
                    
                    // Use the repository to resolve actor
                    var actor = await _repository.GetUserActorAsync(username);
                    if (actor != null)
                    {
                        // Cache the response
                        var webFingerResponse = new WebFingerResponse
                        {
                            Subject = resource,
                            Links = new WebFingerLink[]
                            {
                                new WebFingerLink 
                                { 
                                    Rel = "self", 
                                    Href = actor.Id,
                                    Type = "application/activity+json"
                                }
                            }
                        };
                        
                        await _webFingerCache.SetCachedResponseAsync(cacheKey, webFingerResponse);
                        
                        // Return the actor's WebFinger compatible resource
                        _logger.LogInformation("WebFinger resource resolved and cached successfully for user: {Username}", username);
                        _telemetry.RecordActivityProcessed("WebFingerResolve");
                        return actor.Id;
                    }
                    else
                    {
                        _logger.LogWarning("WebFinger resource not found for user: {Username}", username);
                    }
                }
            }
            
            _logger.LogWarning("WebFinger resource resolution failed for: {Resource}", resource);
            _telemetry.RecordActivityError("WebFingerResolve", new Exception("Resource not found"));
            // For other formats, return as-is or null
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving WebFinger resource: {Resource}", resource);
            _telemetry.RecordActivityError("WebFingerResolve", ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("WebFinger resource resolution completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }
    }
}
