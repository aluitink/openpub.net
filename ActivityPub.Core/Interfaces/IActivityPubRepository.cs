using ActivityPub.Core.Models;
using System.Threading.Tasks;

namespace ActivityPub.Core.Interfaces;

/// <summary>
/// Interface for ActivityPub repository operations
/// </summary>
public interface IActivityPubRepository
{
    /// <summary>
    /// Gets an actor by username
    /// </summary>
    /// <param name="username">The username of the actor</param>
    /// <returns>The actor if found, null otherwise</returns>
    Task<Actor?> GetUserActorAsync(string username);
    
    /// <summary>
    /// Saves an actor
    /// </summary>
    /// <param name="actor">The actor to save</param>
    /// <returns>True if saved successfully, false otherwise</returns>
    Task<bool> SaveUserActorAsync(Actor actor);
    
    /// <summary>
    /// Saves an activity
    /// </summary>
    /// <param name="activity">The activity to save</param>
    /// <returns>True if saved successfully, false otherwise</returns>
    Task<bool> SaveActivityAsync(Activity activity);
    
    /// <summary>
    /// Gets an activity by ID
    /// </summary>
    /// <param name="activityId">The activity ID</param>
    /// <returns>The activity if found, null otherwise</returns>
    Task<Activity?> GetActivityAsync(string activityId);
}