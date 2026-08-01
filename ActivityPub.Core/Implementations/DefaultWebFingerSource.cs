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
    private readonly ILogger<DefaultWebFingerSource> _logger;
    private readonly ActivityPubTelemetry _telemetry;

    public DefaultWebFingerSource(
        IActivityPubRepository repository,
        ActivityPubService activityPubService,
        ILogger<DefaultWebFingerSource> logger,
        ActivityPubTelemetry telemetry)
    {
        _repository = repository;
        _activityPubService = activityPubService;
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
                    
                    // Use the repository to resolve actor
                    var actor = await _repository.GetUserActorAsync(username);
                    if (actor != null)
                    {
                        // Return the actor's WebFinger compatible resource
                        _logger.LogInformation("WebFinger resource resolved successfully for user: {Username}", username);
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