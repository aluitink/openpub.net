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
        var allActivities = await _context.Activities
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.ActivityId, a.JsonData })
            .ToListAsync();

        var activityIds = new List<string>();
        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;

            if (root.TryGetProperty("actor", out var actorElement) &&
                actorElement.ValueKind == JsonValueKind.String &&
                actorElement.GetString() == actorId)
            {
                activityIds.Add(item.ActivityId);
            }
        }

        return activityIds
            .OrderBy(id => id)
            .Skip(skip)
            .Take(limit)
            .ToList();
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

    public async Task<ICollection<SharedInboxDeliveryEntity>> GetPendingSharedInboxDeliveriesAsync(int maxCount = 100)
    {
        var deliveries = await _context.SharedInboxDeliveries
            .Where(d => d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Processing ||
                       (d.Status == DeliveryStatus.Failed && d.RetryCount < 3))
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
        var allActivities = await _context.Activities
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.ActivityId, a.JsonData })
            .ToListAsync();

        var activityIds = new List<string>();
        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;

            if (root.TryGetProperty("to", out var toElement))
            {
                var isTargeted = false;

                if (toElement.ValueKind == JsonValueKind.String && toElement.GetString() == actorId)
                {
                    isTargeted = true;
                }
                else if (toElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var toItem in toElement.EnumerateArray())
                    {
                        if (toItem.ValueKind == JsonValueKind.String && toItem.GetString() == actorId)
                        {
                            isTargeted = true;
                            break;
                        }
                    }
                }

                if (isTargeted)
                {
                    activityIds.Add(item.ActivityId);
                }
            }
        }

        return activityIds
            .Skip(skip)
            .Take(limit)
            .ToList();
    }

    public async Task<ICollection<string>> GetLikedActivitiesAsync(string username, int skip, int limit)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null)
        {
            return new List<string>();
        }

        var actorId = actor.Id;
        var allActivities = await _context.Activities
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.ActivityId, a.JsonData })
            .ToListAsync();

        var likedIds = new List<string>();
        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;

            bool isLike = false;
            if (root.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String &&
                typeElement.GetString() == "Like")
            {
                isLike = true;
            }

            if (!isLike)
            {
                continue;
            }

            bool isByActor = false;
            if (root.TryGetProperty("actor", out var actorElement))
            {
                if (actorElement.ValueKind == JsonValueKind.String && actorElement.GetString() == actorId)
                {
                    isByActor = true;
                }
            }

            if (!isByActor)
            {
                continue;
            }

            if (root.TryGetProperty("object", out var objectElement))
            {
                var likedId = objectElement.ValueKind == JsonValueKind.String
                    ? objectElement.GetString()!
                    : objectElement.GetProperty("id").GetString();

                if (!string.IsNullOrEmpty(likedId))
                {
                    likedIds.Add(likedId);
                }
            }
        }

        return likedIds
            .Skip(skip)
            .Take(limit)
            .ToList();
    }

    public async Task<bool> IsLikedByActorAsync(string username, string targetActivityId)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return false;

        var allActivities = await _context.Activities
            .Select(a => new { a.ActivityId, a.JsonData })
            .ToListAsync();

        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "Like") continue;

            bool isByActor = false;
            if (root.TryGetProperty("actor", out var actorEl))
            {
                if (actorEl.ValueKind == JsonValueKind.String && actorEl.GetString() == actor.Id)
                    isByActor = true;
            }
            if (!isByActor) continue;

            if (root.TryGetProperty("object", out var objEl))
            {
                var likedId = objEl.ValueKind == JsonValueKind.String
                    ? objEl.GetString()!
                    : objEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (likedId == targetActivityId) return true;
            }
        }
        return false;
    }

    public async Task<string?> GetLikeByActorAsync(string username, string targetActivityId)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return null;

        var allActivities = await _context.Activities
            .Select(a => new { a.ActivityId, a.JsonData })
            .ToListAsync();

        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "Like") continue;

            bool isByActor = false;
            if (root.TryGetProperty("actor", out var actorEl))
            {
                if (actorEl.ValueKind == JsonValueKind.String && actorEl.GetString() == actor.Id)
                    isByActor = true;
            }
            if (!isByActor) continue;

            if (root.TryGetProperty("object", out var objEl))
            {
                var likedId = objEl.ValueKind == JsonValueKind.String
                    ? objEl.GetString()!
                    : objEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (likedId == targetActivityId) return item.ActivityId;
            }
        }
        return null;
    }

    public async Task<bool> IsBoostedByActorAsync(string username, string targetActivityId)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return false;

        var allActivities = await _context.Activities
            .Select(a => new { a.ActivityId, a.JsonData })
            .ToListAsync();

        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "Announce") continue;

            bool isByActor = false;
            if (root.TryGetProperty("actor", out var actorEl))
            {
                if (actorEl.ValueKind == JsonValueKind.String && actorEl.GetString() == actor.Id)
                    isByActor = true;
            }
            if (!isByActor) continue;

            if (root.TryGetProperty("object", out var objEl))
            {
                var boostedId = objEl.ValueKind == JsonValueKind.String
                    ? objEl.GetString()!
                    : objEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (boostedId == targetActivityId) return true;
            }
        }
        return false;
    }

    public async Task<string?> GetBoostByActorAsync(string username, string targetActivityId)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null) return null;

        var allActivities = await _context.Activities
            .Select(a => new { a.ActivityId, a.JsonData })
            .ToListAsync();

        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "Announce") continue;

            bool isByActor = false;
            if (root.TryGetProperty("actor", out var actorEl))
            {
                if (actorEl.ValueKind == JsonValueKind.String && actorEl.GetString() == actor.Id)
                    isByActor = true;
            }
            if (!isByActor) continue;

            if (root.TryGetProperty("object", out var objEl))
            {
                var boostedId = objEl.ValueKind == JsonValueKind.String
                    ? objEl.GetString()!
                    : objEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (boostedId == targetActivityId) return item.ActivityId;
            }
        }
        return null;
    }

    public async Task<int> GetLikeCountAsync(string activityId)
    {
        var allActivities = await _context.Activities
            .Select(a => new { a.JsonData })
            .ToListAsync();

        int count = 0;
        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "Like") continue;

            if (root.TryGetProperty("object", out var objEl))
            {
                var likedId = objEl.ValueKind == JsonValueKind.String
                    ? objEl.GetString()!
                    : objEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (likedId == activityId) count++;
            }
        }
        return count;
    }

    public async Task<int> GetBoostCountAsync(string activityId)
    {
        var allActivities = await _context.Activities
            .Select(a => new { a.JsonData })
            .ToListAsync();

        int count = 0;
        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "Announce") continue;

            if (root.TryGetProperty("object", out var objEl))
            {
                var boostedId = objEl.ValueKind == JsonValueKind.String
                    ? objEl.GetString()!
                    : objEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (boostedId == activityId) count++;
            }
        }
        return count;
    }

    public async Task<int> GetReplyCountAsync(string activityId)
    {
        var allActivities = await _context.Activities
            .Select(a => new { a.JsonData })
            .ToListAsync();

        int count = 0;
        foreach (var item in allActivities)
        {
            using var doc = JsonDocument.Parse(item.JsonData);
            var root = doc.RootElement;
            if (root.TryGetProperty("object", out var objEl) && objEl.ValueKind == JsonValueKind.Object)
            {
                if (objEl.TryGetProperty("inReplyTo", out var replyEl) &&
                    (replyEl.ValueKind == JsonValueKind.String || replyEl.ValueKind == JsonValueKind.Object))
                {
                    var replyId = replyEl.ValueKind == JsonValueKind.String
                        ? replyEl.GetString()!
                        : replyEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    if (replyId == activityId) count++;
                }
            }
        }
        return count;
    }
}
