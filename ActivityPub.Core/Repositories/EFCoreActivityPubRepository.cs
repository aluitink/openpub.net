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
        var activities = await _context.Activities
            .OrderBy(a => a.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .Select(a => a.ActivityId)
            .ToListAsync();

        return activities;
    }

    public async Task<ICollection<string>> GetFollowersAsync(string username, int skip, int limit)
    {
        var actor = await GetUserActorAsync(username);
        if (actor == null)
        {
            return new List<string>();
        }

        var followingActivities = await _context.Activities
            .Where(a => a.JsonData.Contains($"\"type\":\"Follow\"") && a.JsonData.Contains($"\"object\":\"{actor.Id}\""))
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

        var followingActivities = await _context.Activities
            .Where(a => a.JsonData.Contains($"\"type\":\"Follow\"") && a.JsonData.Contains($"\"actor\":\"{actor.Id}\""))
            .OrderBy(a => a.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .Select(a => a.JsonData)
            .ToListAsync();

        return followingActivities.Select(json => ExtractActorIdFromFollowJson(json)).Where(id => !string.IsNullOrEmpty(id)).ToList();
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

        var followerActivities = await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Follow\"") && a.JsonData.Contains($"\"object\":\"{actor.Id}\""))
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.JsonData)
            .ToListAsync();

        var followerIds = followerActivities.Select(json => ExtractActorIdFromFollowJson(json)).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

        return followerIds.ToList();
    }
}
