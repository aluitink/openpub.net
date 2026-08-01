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
        _actors[actor.Name] = actor;
        return Task.FromResult(true);
    }
}