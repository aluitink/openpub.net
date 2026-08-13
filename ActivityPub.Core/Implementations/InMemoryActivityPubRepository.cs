using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using System.Threading.Tasks;

namespace ActivityPub.Core.Implementations;

/// <summary>
/// In-memory implementation of the ActivityPub repository
/// </summary>
public class InMemoryActivityPubRepository : IActivityPubRepository
{
    private readonly Dictionary<string, Actor> _actors = new();
    private readonly Dictionary<string, Activity> _activities = new();

    /// <inheritdoc />
    public Task<Actor?> GetUserActorAsync(string username)
    {
        if (_actors.TryGetValue(username, out Actor? actor))
        {
            return Task.FromResult(actor);
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
            return Task.FromResult(activity);
        }
        
        return Task.FromResult<Activity?>(null);
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
}