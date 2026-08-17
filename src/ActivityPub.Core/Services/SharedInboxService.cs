using System.Text.Json;
using ActivityPub.Core.Caching;
using ActivityPub.Core.Events;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActivityPub.Core.Services;

public class SharedInboxService : ISharedInboxService
{
    private readonly IActivityPubRepository _repository;
    private readonly IOutboundActivityService _outboundService;
    private readonly IMemoryCache _cache;
    private readonly IFederationCache _federationCache;
    private readonly ILogger<SharedInboxService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly DeliveryRetryOptions _retryOptions;

    public SharedInboxService(
        IActivityPubRepository repository,
        IOutboundActivityService outboundService,
        IMemoryCache cache,
        IFederationCache federationCache,
        ILogger<SharedInboxService> logger,
        IOptions<ActivityPubOptions>? options = null)
    {
        _repository = repository;
        _outboundService = outboundService;
        _cache = cache;
        _federationCache = federationCache;
        _logger = logger;
        _retryOptions = options?.Value?.DeliveryRetry ?? new DeliveryRetryOptions();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<bool> ProcessIncomingActivityAsync(string username, Activity activity)
    {
        if (activity == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(activity.Id))
        {
            return false;
        }

        if (string.IsNullOrEmpty(activity.Type))
        {
            return false;
        }

        _logger.LogInformation("Processing shared inbox activity {ActivityId} for user {Username}", activity.Id, username);

        if (await _repository.HasSeenActivityAsync(activity.Id))
        {
            _logger.LogInformation("Activity {ActivityId} already seen, skipping duplicate", activity.Id);
            return true;
        }

        await _repository.MarkActivityAsSeenAsync(activity.Id);
        await _repository.SaveActivityAsync(activity);
        await _federationCache.SetActivityAsync(activity.Id, activity);

        _logger.LogInformation("Activity {ActivityId} passed deduplication check and cached", activity.Id);

        var activityJson = JsonSerializer.Serialize(activity, _jsonOptions);

        var followers = await _repository.GetUniqueFollowerIdsAsync(username);

        _logger.LogInformation("Found {FollowerCount} followers for shared inbox distribution", followers.Count);

        foreach (var followerId in followers)
        {
            try
            {
                await _repository.QueueSharedInboxDeliveryAsync(activity.Id, activityJson, followerId);
                _logger.LogDebug("Queued delivery to follower {FollowerId}", followerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue delivery to follower {FollowerId}", followerId);
            }
        }

        return true;
    }

    public async Task<bool> ProcessAndDistributeActivityAsync(string username, Activity activity)
    {
        if (activity == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(activity.Id))
        {
            return false;
        }

        if (string.IsNullOrEmpty(activity.Type))
        {
            return false;
        }

        if (activity.Actor == null)
        {
            return false;
        }

        if (!IsValidActivityType(activity.Type))
        {
            return false;
        }

        _logger.LogInformation("Processing and distributing shared inbox activity {ActivityId} for user {Username}", activity.Id, username);

        if (await _repository.HasSeenActivityAsync(activity.Id))
        {
            _logger.LogInformation("Activity {ActivityId} already seen, skipping duplicate", activity.Id);
            return true;
        }

        await _repository.MarkActivityAsSeenAsync(activity.Id);
        await _repository.SaveActivityAsync(activity);
        await _federationCache.SetActivityAsync(activity.Id, activity);

        _logger.LogInformation("Activity {ActivityId} passed deduplication check and cached", activity.Id);

        var activityJson = JsonSerializer.Serialize(activity, _jsonOptions);

        var followers = await _repository.GetUniqueFollowerIdsAsync(username);

        _logger.LogInformation("Found {FollowerCount} followers for shared inbox distribution", followers.Count);

        foreach (var followerId in followers)
        {
            try
            {
                await _repository.QueueSharedInboxDeliveryAsync(activity.Id, activityJson, followerId);
                _logger.LogDebug("Queued delivery to follower {FollowerId}", followerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue delivery to follower {FollowerId}", followerId);
            }
        }

        return true;
    }

    public async Task<bool> ProcessQueueAsync()
    {
        var maxRetries = Math.Max(1, _retryOptions.MaxRetries);
        var deliveries = await _repository.GetPendingSharedInboxDeliveriesAsync(100, maxRetries);
        var batch = deliveries.Take(50).ToList();

        foreach (var delivery in batch)
        {
            try
            {
                // Both freshly-queued items and previously-failed items that have
                // come out of their backoff window are transitioned to Processing
                // so they get another delivery attempt.
                if (delivery.Status == DeliveryStatus.Queued || delivery.Status == DeliveryStatus.Failed)
                {
                    delivery.Status = DeliveryStatus.Processing;
                    delivery.LastDeliveryAttempt = DateTime.UtcNow;
                    await _repository.UpdateSharedInboxDeliveryAsync(delivery);
                }

                if (delivery.Status == DeliveryStatus.Processing)
                {
                    var activity = JsonSerializer.Deserialize<Activity>(delivery.ActivityJson, _jsonOptions);
                    if (activity == null)
                    {
                        // Malformed payload: retrying will not fix it, so go
                        // straight to the terminal dead-letter state.
                        delivery.Status = DeliveryStatus.MaxRetriesExceeded;
                        delivery.FailureReason = "Failed to deserialize activity";
                        delivery.NextRetryAt = null;
                        await _repository.UpdateSharedInboxDeliveryAsync(delivery);
                        continue;
                    }

                    var success = await _outboundService.SendActivityAsync(
                        delivery.ActivityJson,
                        activity.ActorId ?? string.Empty,
                        string.Empty,
                        delivery.TargetActorId);

                    if (success)
                    {
                        delivery.Status = DeliveryStatus.Delivered;
                        delivery.NextRetryAt = null;
                        _logger.LogInformation("Successfully delivered activity {ActivityId} to {TargetActorId}", delivery.ActivityId, delivery.TargetActorId);
                    }
                    else
                    {
                        HandleDeliveryFailure(delivery, $"Delivery attempt {delivery.RetryCount + 1} failed", maxRetries);
                        _logger.LogWarning("Failed to deliver activity {ActivityId} to {TargetActorId}, retry {RetryCount}, next retry at {NextRetryAt:O}", delivery.ActivityId, delivery.TargetActorId, delivery.RetryCount, delivery.NextRetryAt);
                    }

                    await _repository.UpdateSharedInboxDeliveryAsync(delivery);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing delivery for activity {ActivityId} to {TargetActorId}", delivery.ActivityId, delivery.TargetActorId);

                HandleDeliveryFailure(delivery, ex.Message, maxRetries);
                await _repository.UpdateSharedInboxDeliveryAsync(delivery);
            }
        }

        return true;
    }

    /// <summary>
    /// Records a failed delivery attempt and schedules the next one. Increments
    /// <see cref="SharedInboxDeliveryEntity.RetryCount"/>, and either moves the
    /// item to the terminal <c>MaxRetriesExceeded</c> state (when the retry cap
    /// is reached) or leaves it <c>Failed</c> with a backoff-gated
    /// <see cref="SharedInboxDeliveryEntity.NextRetryAt"/> so the queue processor
    /// will not re-attempt it until the delay has elapsed.
    /// </summary>
    private void HandleDeliveryFailure(SharedInboxDeliveryEntity delivery, string reason, int maxRetries)
    {
        delivery.RetryCount++;
        delivery.LastDeliveryAttempt = DateTime.UtcNow;
        delivery.FailureReason = reason;

        if (delivery.RetryCount >= maxRetries)
        {
            delivery.Status = DeliveryStatus.MaxRetriesExceeded;
            delivery.FailureReason = $"{reason} (max retries ({maxRetries}) exceeded)";
            delivery.NextRetryAt = null;
            _logger.LogWarning("Max retries ({MaxRetries}) exceeded for activity {ActivityId} to {TargetActorId}", maxRetries, delivery.ActivityId, delivery.TargetActorId);
            return;
        }

        delivery.Status = DeliveryStatus.Failed;
        delivery.NextRetryAt = DateTime.UtcNow + ComputeRetryDelay(delivery.RetryCount);
    }

    /// <summary>
    /// Computes the delay before the retry that follows the
    /// <paramref name="attemptNumber"/>-th failed attempt (1-based). With
    /// exponential backoff the delay is
    /// <c>base * 2^(attemptNumber - 1)</c>, capped at
    /// <see cref="DeliveryRetryOptions.MaxRetryDelaySeconds"/>; otherwise a flat
    /// <c>base</c> delay.
    /// </summary>
    private TimeSpan ComputeRetryDelay(int attemptNumber)
    {
        var baseSeconds = Math.Max(1, _retryOptions.BaseRetryDelaySeconds);
        var delaySeconds = _retryOptions.UseExponentialBackoff
            ? baseSeconds * Math.Pow(2, Math.Max(0, attemptNumber - 1))
            : baseSeconds;

        var cap = Math.Max(baseSeconds, _retryOptions.MaxRetryDelaySeconds);
        delaySeconds = Math.Min(delaySeconds, cap);

        return TimeSpan.FromSeconds(delaySeconds);
    }

    public async Task<bool> AddToCacheAsync(string key, string value)
    {
        _cache.Set(key, value, TimeSpan.FromHours(1));
        await _federationCache.SetInboxResponseAsync(key, value);
        return true;
    }

    public async Task<string?> TryGetFromCacheAsync(string key)
    {
        var memoryResult = _cache.Get<string>(key);

        if (memoryResult == null)
        {
            memoryResult = await _federationCache.GetInboxResponseAsync(key);
        }

        return memoryResult;
    }

    public async Task<bool> RemoveFromCacheAsync(string key)
    {
        _cache.Remove(key);
        await _federationCache.RemoveInboxResponseAsync(key);
        return true;
    }

    private bool IsValidActivityType(string? type)
    {
        if (string.IsNullOrEmpty(type))
        {
            return false;
        }

        var typeLower = type.ToLowerInvariant();
        return typeLower switch
        {
            "create" => true,
            "follow" => true,
            "like" => true,
            "announce" => true,
            "undo" => true,
            "accept" => true,
            "reject" => true,
            "delete" => true,
            "update" => true,
            "view" => true,
            _ => false
        };
    }
}

public interface ISharedInboxService
{
    Task<bool> ProcessIncomingActivityAsync(string username, Activity activity);
    Task<bool> ProcessAndDistributeActivityAsync(string username, Activity activity);
    Task<bool> ProcessQueueAsync();
    Task<bool> AddToCacheAsync(string key, string value);
    Task<string?> TryGetFromCacheAsync(string key);
    Task<bool> RemoveFromCacheAsync(string key);
}
