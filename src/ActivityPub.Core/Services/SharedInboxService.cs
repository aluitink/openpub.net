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
    private readonly IPeerHealthService? _peerHealth;

    public SharedInboxService(
        IActivityPubRepository repository,
        IOutboundActivityService outboundService,
        IMemoryCache cache,
        IFederationCache federationCache,
        ILogger<SharedInboxService> logger,
        IOptions<ActivityPubOptions>? options = null,
        IPeerHealthService? peerHealth = null)
    {
        _repository = repository;
        _outboundService = outboundService;
        _cache = cache;
        _federationCache = federationCache;
        _logger = logger;
        _retryOptions = options?.Value?.DeliveryRetry ?? new DeliveryRetryOptions();
        _peerHealth = peerHealth;
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

        // Reject activities from peers that have been blocked for being
        // unreliable (auto- or manually blocked by the peer-health service).
        if (_peerHealth is not null)
        {
            var originDomain = ExtractDomain(activity.ActorId);
            if (!string.IsNullOrEmpty(originDomain) && await _peerHealth.IsDomainBlockedAsync(originDomain))
            {
                _logger.LogInformation(
                    "Rejecting inbound activity {ActivityId} from blocked peer {Domain}",
                    activity.Id, originDomain);
                return false;
            }
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

                    // Determine the recipient domain and whether that peer is
                    // currently blocked by the peer-health service. A blocked
                    // peer is not contacted: the item is left to retry after
                    // backoff (it may be unblocked by then), and no peer-health
                    // failure is recorded for it (the block is already in
                    // effect, so counting it again would be noise).
                    var targetDomain = ExtractDomain(delivery.TargetActorId);
                    var isBlocked = _peerHealth is not null && !string.IsNullOrEmpty(targetDomain)
                        && await _peerHealth.IsDomainBlockedAsync(targetDomain);

                    bool success;
                    if (isBlocked)
                    {
                        success = false;
                        delivery.RetryCount++;
                        delivery.LastDeliveryAttempt = DateTime.UtcNow;
                        delivery.FailureReason = $"Delivery to {targetDomain} skipped (peer is blocked)";
                        delivery.NextRetryAt = DateTime.UtcNow + ComputeRetryDelay(delivery.RetryCount);
                        if (delivery.RetryCount >= maxRetries)
                        {
                            delivery.Status = DeliveryStatus.MaxRetriesExceeded;
                            delivery.FailureReason = $"Delivery to {targetDomain} skipped (peer is blocked; max retries exceeded)";
                            delivery.NextRetryAt = null;
                        }
                        else
                        {
                            delivery.Status = DeliveryStatus.Failed;
                        }
                        _logger.LogInformation("Skipping delivery of activity {ActivityId} to blocked peer {Domain}", delivery.ActivityId, targetDomain);
                        await _repository.UpdateSharedInboxDeliveryAsync(delivery);
                        continue;
                    }

                    // Look up the sender's private key so the outbound request can
                    // be signed. The private key is stored on the local actor in
                    // AdditionalProperties["privateKeyPem"] at registration time.
                    var senderActorId = activity.ActorId ?? string.Empty;
                    var privateKeyPem = await GetPrivateKeyPemAsync(senderActorId);
                    if (string.IsNullOrEmpty(privateKeyPem))
                    {
                        // No private key available — the activity cannot be signed
                        // and therefore cannot be delivered to a remote server.
                        delivery.RetryCount++;
                        delivery.LastDeliveryAttempt = DateTime.UtcNow;
                        delivery.FailureReason = "No private key available for sender actor";
                        if (delivery.RetryCount >= maxRetries)
                        {
                            delivery.Status = DeliveryStatus.MaxRetriesExceeded;
                            delivery.NextRetryAt = null;
                        }
                        else
                        {
                            delivery.Status = DeliveryStatus.Failed;
                            delivery.NextRetryAt = DateTime.UtcNow + ComputeRetryDelay(delivery.RetryCount);
                        }
                        _logger.LogWarning("No private key for sender {SenderActorId}; cannot sign outbound delivery of activity {ActivityId}", senderActorId, delivery.ActivityId);
                        await _repository.UpdateSharedInboxDeliveryAsync(delivery);
                        continue;
                    }

                    success = await _outboundService.SendActivityAsync(
                        delivery.ActivityJson,
                        senderActorId,
                        privateKeyPem,
                        delivery.TargetActorId);

                    // Record the delivery outcome for peer-health tracking so
                    // unreliable peers get auto-blocked and recovering peers get
                    // auto-unblocked.
                    if (_peerHealth is not null && !string.IsNullOrEmpty(targetDomain))
                    {
                        await _peerHealth.RecordDeliveryOutcomeAsync(targetDomain, success);
                    }

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

    /// <summary>
    /// Extracts the host/domain from an actor or inbox URL, or an empty string
    /// when the input is not a valid absolute URI.
    /// </summary>
    private static string ExtractDomain(string? url)
    {
        if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            !string.IsNullOrEmpty(uri.Host))
        {
            return uri.Host;
        }
        return string.Empty;
    }

    /// <summary>
    /// Retrieves the sender actor's private key (PEM) from the local actor
    /// record. The key is stored in <c>AdditionalProperties["privateKeyPem"]</c>
    /// at registration time. Returns null when the actor is not found or has no
    /// private key (e.g. a remote actor).
    /// </summary>
    private async Task<string?> GetPrivateKeyPemAsync(string senderActorId)
    {
        if (string.IsNullOrEmpty(senderActorId))
        {
            return null;
        }

        var username = ExtractUsernameFromActorId(senderActorId);
        if (string.IsNullOrEmpty(username))
        {
            return null;
        }

        var actor = await _repository.GetUserActorAsync(username);
        if (actor?.AdditionalProperties == null ||
            !actor.AdditionalProperties.TryGetValue("privateKeyPem", out var keyElement) ||
            keyElement.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return null;
        }

        return keyElement.GetString();
    }

    /// <summary>
    /// Extracts the username from a local actor ID of the form
    /// <c>https://{domain}/users/{username}</c>. Returns an empty string when
    /// the input does not match the expected local actor URL shape.
    /// </summary>
    private static string ExtractUsernameFromActorId(string actorId)
    {
        if (!Uri.TryCreate(actorId, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var segments = uri.AbsolutePath.TrimStart('/').Split('/');
        if (segments.Length >= 2 && segments[0] == "users")
        {
            return Uri.UnescapeDataString(segments[1]);
        }

        return string.Empty;
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
