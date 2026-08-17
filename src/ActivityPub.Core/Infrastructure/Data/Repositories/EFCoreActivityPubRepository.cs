using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ActivityPub.Core.Implementations;

public class EFCoreActivityPubRepository : IActivityPubRepository
{
    private readonly ActivityPubDbContext _context;
    private readonly JsonSerializerOptions _jsonOptions;

    public EFCoreActivityPubRepository(ActivityPubDbContext context)
    {
        _context = context;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<Actor?> GetUserActorAsync(string username)
    {
        var entity = await _context.Actors
            .Where(a => a.Username == username)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Actor>(entity.JsonData, _jsonOptions);
    }

    public async Task<bool> SaveUserActorAsync(Actor actor)
    {
        var jsonData = JsonSerializer.Serialize(actor, _jsonOptions);
        var username = GetUsernameFromActor(actor);

        var existing = await _context.Actors
            .Where(a => a.Username == username)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.JsonData = jsonData;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.Actors.Update(existing);
        }
        else
        {
            var entity = new ActorEntity
            {
                Username = username,
                JsonData = jsonData,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.Actors.AddAsync(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SaveActivityAsync(Activity activity)
    {
        var jsonData = JsonSerializer.Serialize(activity, _jsonOptions);

        var existing = await _context.Activities
            .Where(a => a.ActivityId == activity.Id)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.JsonData = jsonData;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.Activities.Update(existing);
        }
        else
        {
            var entity = new ActivityEntity
            {
                ActivityId = activity.Id,
                JsonData = jsonData,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.Activities.AddAsync(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ICollection<string>> GetAllActivityIdsAsync()
    {
        return await _context.Activities
            .Select(a => a.ActivityId)
            .ToListAsync();
    }

    public async Task<Activity?> GetActivityAsync(string activityId)
    {
        var entity = await _context.Activities
            .Where(a => a.ActivityId == activityId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Activity>(entity.JsonData, _jsonOptions);
    }

    public async Task<ICollection<string>> GetActorOutboxActivitiesAsync(string username, int skip, int limit)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null)
        {
            return new List<string>();
        }

        var actorId = actor.Id;
        var activityIds = await _context.Activities
            .Where(a => a.JsonData.Contains($"\"actor\":\"{actorId}\""))
            .OrderBy(a => a.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .Select(a => a.ActivityId)
            .ToListAsync();

        return activityIds;
    }

    public async Task<ICollection<string>> GetFollowersAsync(string username, int skip, int limit)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null)
        {
            return new List<string>();
        }

        var actorId = actor.Id;
        var followingActivities = await _context.Activities
            .Where(a => a.JsonData.Contains($"\"type\":\"Follow\"") && a.JsonData.Contains($"\"object\":\"{actorId}\""))
            .OrderBy(a => a.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .Select(a => a.JsonData)
            .ToListAsync();

        return followingActivities.Select(json => ExtractActorIdFromFollowJson(json)).Where(id => !string.IsNullOrEmpty(id)).ToList();
    }

    public async Task<ICollection<string>> GetFollowingAsync(string username, int skip, int limit)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null)
        {
            return new List<string>();
        }

        var actorId = actor.Id;
        var followingActivities = await _context.Activities
            .Where(a => a.JsonData.Contains($"\"type\":\"Follow\"") && a.JsonData.Contains($"\"actor\":\"{actorId}\""))
            .OrderBy(a => a.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .Select(a => a.JsonData)
            .ToListAsync();

        return followingActivities.Select(json => ExtractObjectIdFromFollowJson(json)).Where(id => !string.IsNullOrEmpty(id)).ToList();
    }

    private string ExtractActorIdFromFollowJson(string json)
    {
        try
        {
            if (json.Contains("\"actor\""))
            {
                var start = json.IndexOf("\"actor\"");
                var colon = json.IndexOf(':', start);
                var quote1 = json.IndexOf('"', colon + 1);
                var quote2 = json.IndexOf('"', quote1 + 1);
                if (quote1 > 0 && quote2 > 0)
                {
                    return json.Substring(quote1 + 1, quote2 - quote1 - 1);
                }
            }
        }
        catch
        {
            return string.Empty;
        }
        return string.Empty;
    }

    private string ExtractObjectIdFromFollowJson(string json)
    {
        try
        {
            if (json.Contains("\"object\""))
            {
                var start = json.IndexOf("\"object\"");
                var colon = json.IndexOf(':', start);
                var quote1 = json.IndexOf('"', colon + 1);
                var quote2 = json.IndexOf('"', quote1 + 1);
                if (quote1 > 0 && quote2 > 0)
                {
                    return json.Substring(quote1 + 1, quote2 - quote1 - 1);
                }
            }
        }
        catch
        {
            return string.Empty;
        }
        return string.Empty;
    }

    public async Task<bool> DeleteActivityAsync(string activityId)
    {
        var entity = await _context.Activities
            .Where(a => a.ActivityId == activityId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return false;
        }

        _context.Activities.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    private string GetUsernameFromActor(Actor actor)
    {
        if (!string.IsNullOrEmpty(actor.PreferredUsername))
        {
            return actor.PreferredUsername;
        }

        if (!string.IsNullOrEmpty(actor.Id))
        {
            var segments = actor.Id.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[^1] : actor.Id;
        }

        return actor.Id ?? Guid.NewGuid().ToString();
    }

    public async Task<bool> HasSeenActivityAsync(string activityId)
    {
        var entity = await _context.Activities
            .Where(a => a.ActivityId == activityId)
            .FirstOrDefaultAsync();

        return entity != null;
    }

    public async Task<bool> MarkActivityAsSeenAsync(string activityId)
    {
        var entity = await _context.Activities
            .Where(a => a.ActivityId == activityId)
            .FirstOrDefaultAsync();

        if (entity != null)
        {
            return false;
        }

        var newEntity = new ActivityEntity
        {
            ActivityId = activityId,
            JsonData = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Activities.AddAsync(newEntity);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> QueueSharedInboxDeliveryAsync(string activityId, string activityJson, string targetActorId)
    {
        var existing = await _context.SharedInboxDeliveries
            .Where(d => d.ActivityId == activityId && d.TargetActorId == targetActorId)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            if (existing.Status == DeliveryStatus.Failed || existing.Status == DeliveryStatus.MaxRetriesExceeded)
            {
                existing.Status = DeliveryStatus.Queued;
                existing.RetryCount = 0;
                existing.FailureReason = null;
                existing.NextRetryAt = null;
                _context.SharedInboxDeliveries.Update(existing);
            }
            else
            {
                return false;
            }
        }
        else
        {
            var delivery = new SharedInboxDeliveryEntity
            {
                Id = $"{activityId}-{targetActorId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                ActivityId = activityId,
                ActivityJson = activityJson,
                TargetActorId = targetActorId,
                Status = DeliveryStatus.Queued,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.SharedInboxDeliveries.AddAsync(delivery);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ICollection<SharedInboxDeliveryEntity>> GetPendingSharedInboxDeliveriesAsync(int maxCount = 100, int maxRetries = 5)
    {
        var now = DateTime.UtcNow;

        var deliveries = await _context.SharedInboxDeliveries
            .Where(d => d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Processing ||
                       (d.Status == DeliveryStatus.Failed &&
                        d.RetryCount < maxRetries &&
                        (d.NextRetryAt == null || d.NextRetryAt <= now)))
            .OrderBy(d => d.CreatedAt)
            .Take(maxCount)
            .ToListAsync();

        return deliveries;
    }

    public async Task<bool> UpdateSharedInboxDeliveryAsync(SharedInboxDeliveryEntity delivery)
    {
        _context.SharedInboxDeliveries.Update(delivery);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ICollection<string>> GetUniqueFollowerIdsAsync(string username)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null)
        {
            return new List<string>();
        }

        var actorId = actor.Id;
        var followerActivities = await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Follow\"") && a.JsonData.Contains($"\"object\":\"{actorId}\""))
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.JsonData)
            .ToListAsync();

        var followerIds = followerActivities.Select(json => ExtractActorIdFromFollowJson(json)).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

        return followerIds.ToList();
    }

    public async Task<int> GetFollowerCountAsync(string username)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return 0;

        return await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Follow\"") && a.JsonData.Contains($"\"object\":\"{actor.Id}\""))
            .CountAsync();
    }

    public async Task<int> GetFollowingCountAsync(string username)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return 0;

        return await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Follow\"") && a.JsonData.Contains($"\"actor\":\"{actor.Id}\""))
            .CountAsync();
    }

    public async Task<int> GetNoteCountAsync(string username)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return 0;

        return await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Create\"")
                && a.JsonData.Contains($"\"actor\":\"{actor.Id}\"")
                && a.JsonData.Contains("\"type\":\"Note\""))
            .CountAsync();
    }

    public async Task<bool> IsFollowingAsync(string followerUsername, string targetActorId)
    {
        var follower = await GetUserActorAsync(followerUsername);
        if (follower == null || string.IsNullOrEmpty(targetActorId)) return false;

        var followerId = follower.Id;

        // A genuine follow is a top-level Follow activity authored by this user
        // whose object is exactly the target actor. An Undo's JSON also contains
        // the target (in its embedded Follow) plus the text "type":"Follow", so
        // we exclude any activity whose top-level type is "Undo".
        return await _context.Activities
            .AnyAsync(a => a.JsonData.Contains("\"type\":\"Follow\"")
                && a.JsonData.Contains($"\"actor\":\"{followerId}\"")
                && a.JsonData.Contains($"\"object\":\"{targetActorId}\"")
                && !a.JsonData.Contains("\"type\":\"Undo\""));
    }

    public async Task<bool> SaveWebhookConfigAsync(WebhookConfigEntity config)
    {
        var existing = await _context.WebhookConfigs
            .Where(c => c.Id == config.Id)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.EndpointUrl = config.EndpointUrl;
            existing.HttpMethod = config.HttpMethod;
            existing.Enabled = config.Enabled;
            existing.SecretKey = config.SecretKey;
            existing.MaxRetries = config.MaxRetries;
            existing.RetryDelaySeconds = config.RetryDelaySeconds;
            existing.UseExponentialBackoff = config.UseExponentialBackoff;
            existing.EventType = config.EventType;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.WebhookConfigs.Update(existing);
        }
        else
        {
            config.CreatedAt = DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;
            await _context.WebhookConfigs.AddAsync(config);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ICollection<WebhookConfigEntity>> GetWebhookConfigsAsync(string actorId, string? eventType = null)
    {
        var query = _context.WebhookConfigs.Where(c => c.ActorId == actorId);

        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(c => c.EventType == eventType);
        }

        return await query.ToListAsync();
    }

    public async Task<WebhookConfigEntity?> GetWebhookConfigByIdAsync(int id)
    {
        return await _context.WebhookConfigs.FindAsync(id);
    }

    public async Task<bool> DeleteWebhookConfigAsync(int id)
    {
        var config = await _context.WebhookConfigs.FindAsync(id);
        if (config != null)
        {
            _context.WebhookConfigs.Remove(config);
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task<bool> QueueWebhookDeliveryAsync(WebhookDeliveryEntity delivery)
    {
        delivery.Id = Guid.NewGuid().ToString();
        delivery.Status = WebhookDeliveryStatus.Queued;
        delivery.RetryCount = 0;
        delivery.CreatedAt = DateTime.UtcNow;
        delivery.UpdatedAt = DateTime.UtcNow;

        await _context.WebhookDeliveries.AddAsync(delivery);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ICollection<WebhookDeliveryEntity>> GetPendingWebhookDeliveriesAsync(int maxCount = 100)
    {
        var deliveries = await _context.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Queued || d.Status == WebhookDeliveryStatus.Processing ||
                       (d.Status == WebhookDeliveryStatus.Failed && d.RetryCount < 3))
            .OrderBy(d => d.CreatedAt)
            .Take(maxCount)
            .ToListAsync();

        return deliveries;
    }

    public async Task<bool> UpdateWebhookDeliveryAsync(WebhookDeliveryEntity delivery)
    {
        _context.WebhookDeliveries.Update(delivery);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SaveWebhookDeliveryHistoryAsync(WebhookDeliveryHistoryEntity history)
    {
        history.Id = Guid.NewGuid().ToString();
        history.Timestamp = DateTime.UtcNow;

        await _context.WebhookDeliveryHistories.AddAsync(history);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ICollection<string>> GetInboxActivitiesAsync(string username, int skip, int limit)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null)
        {
            return new List<string>();
        }

        var actorId = actor.Id;
        var activityIds = await _context.Activities
            .Where(a => a.JsonData.Contains($"\"to\":\"{actorId}\"") || a.JsonData.Contains($"\"{actorId}\""))
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .Select(a => a.ActivityId)
            .ToListAsync();

        return activityIds;
    }

    public async Task<ICollection<string>> GetLikedActivitiesAsync(string username, int skip, int limit)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null)
        {
            return new List<string>();
        }

        var actorId = actor.Id;
        var likeActivities = await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Like\"") && a.JsonData.Contains($"\"actor\":\"{actorId}\""))
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .Select(a => new { a.ActivityId, a.JsonData })
            .ToListAsync();

        var likedIds = new List<string>();
        foreach (var item in likeActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;
            if (root.TryGetProperty("object", out var objectElement))
            {
                var likedId = objectElement.ValueKind == JsonValueKind.String
                    ? objectElement.GetString()!
                    : objectElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

                if (!string.IsNullOrEmpty(likedId))
                {
                    likedIds.Add(likedId);
                }
            }
        }

        return likedIds;
    }

    public async Task<bool> IsLikedByActorAsync(string username, string targetActivityId)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return false;

        var count = await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Like\"") &&
                        a.JsonData.Contains($"\"actor\":\"{actor.Id}\"") &&
                        a.JsonData.Contains($"\"{targetActivityId}\""))
            .CountAsync();

        return count > 0;
    }

    public async Task<string?> GetLikeByActorAsync(string username, string targetActivityId)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return null;

        var likeActivity = await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Like\"") &&
                        a.JsonData.Contains($"\"actor\":\"{actor.Id}\"") &&
                        a.JsonData.Contains($"\"{targetActivityId}\""))
            .Select(a => a.ActivityId)
            .FirstOrDefaultAsync();

        return likeActivity;
    }

    public async Task<bool> IsBoostedByActorAsync(string username, string targetActivityId)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return false;

        var count = await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Announce\"") &&
                        a.JsonData.Contains($"\"actor\":\"{actor.Id}\"") &&
                        a.JsonData.Contains($"\"{targetActivityId}\""))
            .CountAsync();

        return count > 0;
    }

    public async Task<string?> GetBoostByActorAsync(string username, string targetActivityId)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return null;

        var boostActivity = await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Announce\"") &&
                        a.JsonData.Contains($"\"actor\":\"{actor.Id}\"") &&
                        a.JsonData.Contains($"\"{targetActivityId}\""))
            .Select(a => a.ActivityId)
            .FirstOrDefaultAsync();

        return boostActivity;
    }

    public async Task<int> GetLikeCountAsync(string activityId)
    {
        return await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Like\"") &&
                        a.JsonData.Contains($"\"{activityId}\""))
            .CountAsync();
    }

    public async Task<int> GetBoostCountAsync(string activityId)
    {
        return await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Announce\"") &&
                        a.JsonData.Contains($"\"{activityId}\""))
            .CountAsync();
    }

    public async Task<int> GetReplyCountAsync(string activityId)
    {
        return await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Reply\"") ||
                        a.JsonData.Contains("\"inReplyTo\""))
            .Where(a => a.JsonData.Contains($"\"{activityId}\""))
            .CountAsync();
    }

    public async Task<FederationPeerEntity?> GetFederationPeerAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return null;
        return await _context.FederationPeers.FirstOrDefaultAsync(p => p.Domain == domain);
    }

    public async Task<bool> SaveFederationPeerAsync(FederationPeerEntity peer)
    {
        if (peer == null || string.IsNullOrEmpty(peer.Domain)) return false;

        var existing = await _context.FederationPeers.FirstOrDefaultAsync(p => p.Domain == peer.Domain);
        if (existing == null)
        {
            peer.CreatedAt = DateTime.UtcNow;
            peer.UpdatedAt = DateTime.UtcNow;
            await _context.FederationPeers.AddAsync(peer);
        }
        else
        {
            existing.ConsecutiveFailures = peer.ConsecutiveFailures;
            existing.ConsecutiveSuccesses = peer.ConsecutiveSuccesses;
            existing.TotalDeliveries = peer.TotalDeliveries;
            existing.TotalFailures = peer.TotalFailures;
            existing.LastDeliveryAttempt = peer.LastDeliveryAttempt;
            existing.LastSuccessfulDelivery = peer.LastSuccessfulDelivery;
            existing.LastProbedAt = peer.LastProbedAt;
            existing.LastProbeReachable = peer.LastProbeReachable;
            existing.ConsecutiveProbeFailures = peer.ConsecutiveProbeFailures;
            existing.IsBlocked = peer.IsBlocked;
            existing.BlockedAt = peer.BlockedAt;
            existing.BlockedReason = peer.BlockedReason;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.FederationPeers.Update(existing);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ICollection<FederationPeerEntity>> GetFederationPeersAsync(bool onlyBlocked = false)
    {
        var query = _context.FederationPeers.AsQueryable();
        if (onlyBlocked)
        {
            query = query.Where(p => p.IsBlocked);
        }
        return await query.OrderBy(p => p.Domain).ToListAsync();
    }

    public async Task<ICollection<string>> GetBlockedDomainNamesAsync()
    {
        return await _context.FederationPeers
            .Where(p => p.IsBlocked)
            .Select(p => p.Domain)
            .ToListAsync();
    }
}
