using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using System.Threading.Tasks;

namespace ActivityPub.Core.Implementations;

/// <summary>
/// In-memory implementation of the ActivityPub repository
/// </summary>
public class InMemoryActivityPubRepository : IActivityPubRepository
{
    private readonly Dictionary<string, Actor> _actors = new();
    private readonly Dictionary<string, Activity> _activities = new();
    private readonly HashSet<string> _seenActivities = new();
    private readonly List<SharedInboxDeliveryEntity> _sharedInboxDeliveries = new();

    /// <inheritdoc />
    public Task<Actor?> GetUserActorAsync(string username)
    {
        if (_actors.TryGetValue(username, out Actor? actor))
        {
            return Task.FromResult<Actor?>(actor);
        }
        
        return Task.FromResult<Actor?>(null);
    }

    /// <inheritdoc />
    public Task<bool> SaveUserActorAsync(Actor actor)
    {
        var username = GetUsernameFromActor(actor);
        _actors[username] = actor;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> SaveActivityAsync(Activity activity)
    {
        _activities[activity.Id] = activity;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<Activity?> GetActivityAsync(string activityId)
    {
        if (_activities.TryGetValue(activityId, out Activity? activity))
        {
            return Task.FromResult<Activity?>(activity);
        }
        
        return Task.FromResult<Activity?>(null);
    }

    /// <inheritdoc />
    public Task<ICollection<string>> GetActorOutboxActivitiesAsync(string username, int skip, int limit)
    {
        var activityIds = _activities.Values
            .OrderBy(a => a.Published ?? DateTime.MinValue)
            .Skip(skip)
            .Take(limit)
            .Select(a => a.Id)
            .ToList();
        
        return Task.FromResult<ICollection<string>>(activityIds);
    }

    /// <inheritdoc />
    public Task<ICollection<string>> GetFollowersAsync(string username, int skip, int limit)
    {
        var followers = new List<string>();
        return Task.FromResult<ICollection<string>>(followers);
    }

    /// <inheritdoc />
    public Task<ICollection<string>> GetFollowingAsync(string username, int skip, int limit)
    {
        var following = new List<string>();
        return Task.FromResult<ICollection<string>>(following);
    }

    /// <inheritdoc />
    public Task<bool> DeleteActivityAsync(string activityId)
    {
        if (_activities.Remove(activityId))
        {
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> HasSeenActivityAsync(string activityId)
    {
        return Task.FromResult(_seenActivities.Contains(activityId));
    }

    /// <inheritdoc />
    public Task<bool> MarkActivityAsSeenAsync(string activityId)
    {
        _seenActivities.Add(activityId);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> QueueSharedInboxDeliveryAsync(string activityId, string activityJson, string targetActorId)
    {
        var delivery = new SharedInboxDeliveryEntity
        {
            Id = Guid.NewGuid().ToString(),
            ActivityId = activityId,
            ActivityJson = activityJson,
            TargetActorId = targetActorId,
            Status = DeliveryStatus.Queued,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        _sharedInboxDeliveries.Add(delivery);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<ICollection<SharedInboxDeliveryEntity>> GetPendingSharedInboxDeliveriesAsync(int maxCount = 100)
    {
        var pending = _sharedInboxDeliveries
            .Where(d => d.Status == DeliveryStatus.Queued || 
                       d.Status == DeliveryStatus.Failed)
            .Take(maxCount)
            .ToList();
        return Task.FromResult<ICollection<SharedInboxDeliveryEntity>>(pending);
    }

    /// <inheritdoc />
    public Task<bool> UpdateSharedInboxDeliveryAsync(SharedInboxDeliveryEntity delivery)
    {
        var existing = _sharedInboxDeliveries.FirstOrDefault(d => d.Id == delivery.Id);
        if (existing != null)
        {
            var index = _sharedInboxDeliveries.IndexOf(existing);
            _sharedInboxDeliveries[index] = delivery;
        }
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<ICollection<string>> GetUniqueFollowerIdsAsync(string username)
    {
        var followers = new List<string>();
        return Task.FromResult<ICollection<string>>(followers);
    }

    private static string GetUsernameFromActor(Actor actor)
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
}