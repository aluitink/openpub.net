using System.Text.Json;
using ActivityPub.Core.Caching;
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
    private readonly IFederationCache _federationCache;
    private readonly ILogger<SharedInboxService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SharedInboxService(
        IActivityPubRepository repository,
        IOutboundActivityService outboundService,
        IMemoryCache cache,
        IFederationCache federationCache,
        ILogger<SharedInboxService> logger)
    {
        _repository = repository;
        _outboundService = outboundService;
        _cache = cache;
        _federationCache = federationCache;
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
        var deliveries = await _repository.GetPendingSharedInboxDeliveriesAsync(100);
        var batch = deliveries.Take(50).ToList();

        foreach (var delivery in batch)
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
