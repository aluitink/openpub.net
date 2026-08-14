using System.Text.Json;
using ActivityPub.Core.Events;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Services;

public class SharedInboxService : ISharedInboxService
{
    private readonly IActivityPubRepository _repository;
    private readonly IOutboundActivityService _outboundService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SharedInboxService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SharedInboxService(
        IActivityPubRepository repository,
        IOutboundActivityService outboundService,
        IMemoryCache cache,
        ILogger<SharedInboxService> logger)
    {
        _repository = repository;
        _outboundService = outboundService;
        _cache = cache;
        _logger = logger;
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
            throw new ArgumentNullException(nameof(activity));
        }

        if (string.IsNullOrEmpty(activity.Id))
        {
            throw new ArgumentException("Activity must have an ID", nameof(activity.Id));
        }

        if (string.IsNullOrEmpty(activity.Type))
        {
            throw new ArgumentException("Activity must have a type", nameof(activity.Type));
        }

        _logger.LogInformation("Processing shared inbox activity {ActivityId} for user {Username}", activity.Id, username);

        if (await _repository.HasSeenActivityAsync(activity.Id))
        {
            _logger.LogInformation("Activity {ActivityId} already seen, skipping duplicate", activity.Id);
            return true;
        }

        await _repository.MarkActivityAsSeenAsync(activity.Id);

        _logger.LogInformation("Activity {ActivityId} passed deduplication check", activity.Id);

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
            throw new ArgumentNullException(nameof(activity));
        }

        if (string.IsNullOrEmpty(activity.Id))
        {
            throw new ArgumentException("Activity must have an ID", nameof(activity.Id));
        }

        if (string.IsNullOrEmpty(activity.Type))
        {
            throw new ArgumentException("Activity must have a type", nameof(activity.Type));
        }

        _logger.LogInformation("Processing and distributing shared inbox activity {ActivityId} for user {Username}", activity.Id, username);

        if (await _repository.HasSeenActivityAsync(activity.Id))
        {
            _logger.LogInformation("Activity {ActivityId} already seen, skipping duplicate", activity.Id);
            return true;
        }

        await _repository.MarkActivityAsSeenAsync(activity.Id);

        _logger.LogInformation("Activity {ActivityId} passed deduplication check", activity.Id);

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
        var deliveries = await _repository.GetPendingSharedInboxDeliveriesAsync(100);

        foreach (var delivery in deliveries)
        {
            try
            {
                if (delivery.Status == DeliveryStatus.Queued)
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
                        delivery.Status = DeliveryStatus.MaxRetriesExceeded;
                        delivery.FailureReason = "Failed to deserialize activity";
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
                        _logger.LogInformation("Successfully delivered activity {ActivityId} to {TargetActorId}", delivery.ActivityId, delivery.TargetActorId);
                    }
                    else
                    {
                        delivery.RetryCount++;
                        if (delivery.RetryCount >= 3)
                        {
                            delivery.Status = DeliveryStatus.MaxRetriesExceeded;
                            delivery.FailureReason = "Max retries (3) exceeded";
                            _logger.LogWarning("Max retries exceeded for activity {ActivityId} to {TargetActorId}", delivery.ActivityId, delivery.TargetActorId);
                        }
                        else
                        {
                            delivery.Status = DeliveryStatus.Failed;
                            delivery.FailureReason = $"Delivery attempt {delivery.RetryCount} failed";
                            _logger.LogWarning("Failed to deliver activity {ActivityId} to {TargetActorId}, retry {RetryCount}", delivery.ActivityId, delivery.TargetActorId, delivery.RetryCount);
                        }
                    }

                    await _repository.UpdateSharedInboxDeliveryAsync(delivery);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing delivery for activity {ActivityId} to {TargetActorId}", delivery.ActivityId, delivery.TargetActorId);
                
                delivery.RetryCount++;
                if (delivery.RetryCount >= 3)
                {
                    delivery.Status = DeliveryStatus.MaxRetriesExceeded;
                    delivery.FailureReason = ex.Message;
                }
                else
                {
                    delivery.Status = DeliveryStatus.Failed;
                    delivery.FailureReason = ex.Message;
                }
                
                await _repository.UpdateSharedInboxDeliveryAsync(delivery);
            }
        }

        return true;
    }

    public async Task<bool> AddToCacheAsync(string key, string value)
    {
        _cache.Set(key, value, TimeSpan.FromHours(1));
        return true;
    }

    public async Task<string?> TryGetFromCacheAsync(string key)
    {
        return _cache.Get<string>(key);
    }

    public async Task<bool> RemoveFromCacheAsync(string key)
    {
        _cache.Remove(key);
        return true;
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
