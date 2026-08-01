using ActivityPub.Core.Events;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;
using ActivityPub.Core.Infrastructure.Telemetry;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ActivityPub.Core.Interfaces;

namespace ActivityPub.Core.Services;

/// <summary>
/// Central service for ActivityPub operations with event hook support
/// </summary>
public class ActivityPubService
{
    private readonly IActivityPubRepository _repository;
    private readonly ActivityPubEventDispatcher _eventDispatcher;
    private readonly IEnumerable<IActivityPubInterceptor> _interceptors;
    private readonly ILogger<ActivityPubService> _logger;
    private readonly ActivityPubTelemetry _telemetry;

    public ActivityPubService(
        IActivityPubRepository repository,
        ActivityPubEventDispatcher eventDispatcher,
        IEnumerable<IActivityPubInterceptor> interceptors,
        ILogger<ActivityPubService> logger,
        ActivityPubTelemetry telemetry)
    {
        _repository = repository;
        _eventDispatcher = eventDispatcher;
        _interceptors = interceptors;
        _logger = logger;
        _telemetry = telemetry;
    }

/// <summary>
/// Gets an actor with event processing
/// </summary>
/// <param name="username">The username to lookup</param>
/// <returns>The actor if found, null otherwise</returns>
public async Task<Actor?> GetActorWithEventAsync(string username)
{
    var stopwatch = Stopwatch.StartNew();
    _logger.LogInformation("GetActorWithEventAsync called for username: {Username}", username);
    
    try
    {
        // Check interceptors first
        foreach (var interceptor in _interceptors)
        {
            // In a real implementation, you'd call the interceptor
            // For now, we'll just return from the repository
        }
        
        var actor = await _repository.GetUserActorAsync(username);
        _logger.LogInformation("GetActorWithEventAsync successful for username: {Username}", username);
        _telemetry.RecordActivityProcessed("GetActor");
        _telemetry.RecordHttpRequestProcessed("GET", $"/users/{username}", 200, stopwatch.ElapsedMilliseconds);
        
        return actor;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in GetActorWithEventAsync for username: {Username}", username);
        _telemetry.RecordActivityError("GetActor", ex);
        _telemetry.RecordHttpRequestError("GET", $"/users/{username}", 500, ex);
        throw;
    }
    finally
    {
        stopwatch.Stop();
        _logger.LogDebug("GetActorWithEventAsync completed for username: {Username} in {ElapsedMilliseconds} ms", username, stopwatch.ElapsedMilliseconds);
    }
}
    
/// <summary>
/// Processes an incoming activity through event hooks
/// </summary>
/// <param name="activity">The activity to process</param>
/// <returns>True if processing should continue, false to cancel</returns>
[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Method signature must match interface")]
public async Task<bool> ProcessIncomingActivityAsync(Activity activity)
{
    var stopwatch = Stopwatch.StartNew();
    _logger.LogInformation("ProcessIncomingActivityAsync called for activity: {ActivityId}", activity.Id);
    
    try
    {
        // Apply interceptors
        foreach (var interceptor in _interceptors)
        {
            var shouldContinue = await interceptor.OnActivityReceivedAsync(activity);
            if (!shouldContinue)
            {
                _logger.LogWarning("Activity processing cancelled by interceptor for activity: {ActivityId}", activity.Id);
                return false;
            }
        }
        
        // Dispatch event
        var eventObj = new ActivityReceivedEvent(activity);
        await _eventDispatcher.DispatchAsync(eventObj);
        
        _logger.LogInformation("ProcessIncomingActivityAsync successful for activity: {ActivityId}", activity.Id);
        _telemetry.RecordActivityProcessed("ProcessIncomingActivity");
        _telemetry.RecordHttpRequestProcessed("POST", "/inbox", 200, stopwatch.ElapsedMilliseconds);
        
        return true;
    }
    catch (Exception ex)
    {
        _是.LogWarning("Error in ProcessIncomingActivityAsync for activity: {ActivityId}", activity.Id);
        _telemetry.RecordActivityError("ProcessIncomingActivity", ex);
        _telemetry.RecordHttpRequestError("POST", "/inbox", 500, ex);
        throw;
    }
    finally
    {
        stopwatch.Stop();
        _logger.LogDebug("ProcessIncomingActivityAsync completed for activity: {ActivityId} in {ElapsedMilliseconds} ms", activity.Id, stopwatch.ElapsedMilliseconds);
    }
}
}