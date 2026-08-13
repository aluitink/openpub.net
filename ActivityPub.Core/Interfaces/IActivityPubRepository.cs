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
    
    /// <summary>
    /// Gets outbox activities for an actor
    /// </summary>
    /// <param name="username">The username of the actor</param>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <returns>Collection of activity IDs</returns>
    Task<ICollection<string>> GetActorOutboxActivitiesAsync(string username, int skip, int limit);
    
    /// <summary>
    /// Gets followers for an actor
    /// </summary>
    /// <param name="username">The username of the actor</param>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <returns>Collection of follower actor IDs</returns>
    Task<ICollection<string>> GetFollowersAsync(string username, int skip, int limit);
    
    /// <summary>
    /// Gets actors that the given user is following
    /// </summary>
    /// <param name="username">The username of the actor</param>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <returns>Collection of following actor IDs</returns>
    Task<ICollection<string>> GetFollowingAsync(string username, int skip, int limit);
    
    /// <summary>
    /// Marks an activity as deleted (tombstone)
    /// </summary>
    /// <param name="activityId">The activity ID to delete</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteActivityAsync(string activityId);
}