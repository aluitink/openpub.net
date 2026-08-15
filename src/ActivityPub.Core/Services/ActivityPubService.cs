using ActivityPub.Core.Caching;
using ActivityPub.Core.Events;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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
    private readonly IFederationCache _cache;
    private readonly CacheInvalidationService _invalidationService;

    public ActivityPubService(
        IActivityPubRepository repository,
        ActivityPubEventDispatcher eventDispatcher,
        IEnumerable<IActivityPubInterceptor> interceptors,
        ILogger<ActivityPubService> logger,
        IFederationCache cache,
        CacheInvalidationService invalidationService)
    {
        _repository = repository;
        _eventDispatcher = eventDispatcher;
        _interceptors = interceptors;
        _logger = logger;
        _cache = cache;
        _invalidationService = invalidationService;
    }

    public async Task<Actor?> GetActorWithEventAsync(string username)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("GetActorWithEventAsync called for username: {Username}", username);

        try
        {
            foreach (var interceptor in _interceptors)
            {
            }

            var actor = await _repository.GetUserActorAsync(username);

            if (actor != null)
            {
                await _cache.SetActorAsync(actor.Id, actor);
                _logger.LogInformation("Actor cached for username: {Username}", username);
            }

            _logger.LogInformation("GetActorWithEventAsync successful for username: {Username}", username);

            return actor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetActorWithEventAsync for username: {Username}", username);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("GetActorWithEventAsync completed for username: {Username} in {ElapsedMilliseconds} ms", username, stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<bool> ProcessIncomingActivityAsync(ActivityPub.Core.Models.Activity activity)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("ProcessIncomingActivityAsync called for activity: {ActivityId}", activity.Id);

        try
        {
            foreach (var interceptor in _interceptors)
            {
                var shouldContinue = await interceptor.OnActivityReceivedAsync(activity);
                if (!shouldContinue)
                {
                    _logger.LogWarning("Activity processing cancelled by interceptor for activity: {ActivityId}", activity.Id);
                    return false;
                }
            }

            var eventObj = new ActivityReceivedEvent(activity);
            await _eventDispatcher.DispatchAsync(eventObj);

            if (activity.Id != null)
            {
                await _cache.SetActivityAsync(activity.Id, activity);
                _logger.LogInformation("Activity cached for ID: {ActivityId}", activity.Id);
            }

            _logger.LogInformation("ProcessIncomingActivityAsync successful for activity: {ActivityId}", activity.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessIncomingActivityAsync for activity: {ActivityId}", activity.Id);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("ProcessIncomingActivityAsync completed for activity: {ActivityId} in {ElapsedMilliseconds} ms", activity.Id, stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<bool> InvalidateActorCacheAsync(string actorId)
    {
        _logger.LogInformation("Invalidating cache for actor: {ActorId}", actorId);

        await _invalidationService.InvalidateAllForActorAsync(actorId);

        return true;
    }
}
