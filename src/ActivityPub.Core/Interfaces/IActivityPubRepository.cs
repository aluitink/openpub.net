using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
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

    /// <summary>
    /// Checks if an activity has been seen before (deduplication)
    /// </summary>
    /// <param name="activityId">The activity ID to check</param>
    /// <returns>True if the activity has been seen, false otherwise</returns>
    Task<bool> HasSeenActivityAsync(string activityId);

    /// <summary>
    /// Marks an activity as seen (for deduplication)
    /// </summary>
    /// <param name="activityId">The activity ID to mark as seen</param>
    /// <returns>True if marked successfully, false if already seen</returns>
    Task<bool> MarkActivityAsSeenAsync(string activityId);

    /// <summary>
    /// Queues a shared inbox delivery for a target actor
    /// </summary>
    /// <param name="activityId">The activity ID</param>
    /// <param name="activityJson">The activity JSON data</param>
    /// <param name="targetActorId">The target actor ID</param>
    /// <returns>True if queued successfully, false otherwise</returns>
    Task<bool> QueueSharedInboxDeliveryAsync(string activityId, string activityJson, string targetActorId);

    /// <summary>
    /// Gets pending shared inbox deliveries
    /// </summary>
    /// <param name="maxCount">Maximum number of deliveries to retrieve</param>
    /// <returns>Collection of pending deliveries</returns>
    Task<ICollection<SharedInboxDeliveryEntity>> GetPendingSharedInboxDeliveriesAsync(int maxCount = 100);

    /// <summary>
    /// Updates a shared inbox delivery status
    /// </summary>
    /// <param name="delivery">The delivery entity to update</param>
    /// <returns>True if updated successfully, false otherwise</returns>
    Task<bool> UpdateSharedInboxDeliveryAsync(SharedInboxDeliveryEntity delivery);

    /// <summary>
    /// Gets unique follower actor IDs for a username (for shared inbox distribution)
    /// </summary>
    /// <param name="username">The username of the actor</param>
    /// <returns>Collection of unique follower actor IDs</returns>
    Task<ICollection<string>> GetUniqueFollowerIdsAsync(string username);

    /// <summary>
    /// Saves a webhook configuration
    /// </summary>
    /// <param name="config">The webhook configuration to save</param>
    /// <returns>True if saved successfully, false otherwise</returns>
    Task<bool> SaveWebhookConfigAsync(WebhookConfigEntity config);

    /// <summary>
    /// Gets webhook configurations for an actor
    /// </summary>
    /// <param name="actorId">The actor ID</param>
    /// <param name="eventType">Optional event type filter</param>
    /// <returns>Collection of webhook configurations</returns>
    Task<ICollection<WebhookConfigEntity>> GetWebhookConfigsAsync(string actorId, string? eventType = null);

    /// <summary>
    /// Gets a webhook configuration by ID
    /// </summary>
    /// <param name="id">The configuration ID</param>
    /// <returns>The webhook configuration if found, null otherwise</returns>
    Task<WebhookConfigEntity?> GetWebhookConfigByIdAsync(int id);

    /// <summary>
    /// Deletes a webhook configuration
    /// </summary>
    /// <param name="id">The configuration ID</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteWebhookConfigAsync(int id);

    /// <summary>
    /// Queues a webhook delivery
    /// </summary>
    /// <param name="delivery">The webhook delivery entity</param>
    /// <returns>True if queued successfully, false otherwise</returns>
    Task<bool> QueueWebhookDeliveryAsync(WebhookDeliveryEntity delivery);

    /// <summary>
    /// Gets pending webhook deliveries
    /// </summary>
    /// <param name="maxCount">Maximum number of deliveries to retrieve</param>
    /// <returns>Collection of pending deliveries</returns>
    Task<ICollection<WebhookDeliveryEntity>> GetPendingWebhookDeliveriesAsync(int maxCount = 100);

    /// <summary>
    /// Updates a webhook delivery status
    /// </summary>
    /// <param name="delivery">The delivery entity to update</param>
    /// <returns>True if updated successfully, false otherwise</returns>
    Task<bool> UpdateWebhookDeliveryAsync(WebhookDeliveryEntity delivery);

    /// <summary>
    /// Saves webhook delivery history
    /// </summary>
    /// <param name="history">The delivery history entity</param>
    /// <returns>True if saved successfully, false otherwise</returns>
    Task<bool> SaveWebhookDeliveryHistoryAsync(WebhookDeliveryHistoryEntity history);

    /// <summary>
    /// Gets inbox activities for an actor
    /// </summary>
    /// <param name="username">The username of the actor</param>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <returns>Collection of activity IDs</returns>
    Task<ICollection<string>> GetInboxActivitiesAsync(string username, int skip, int limit);

    /// <summary>
    /// Gets liked activity IDs for an actor
    /// </summary>
    /// <param name="username">The username of the actor</param>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <returns>Collection of liked activity IDs</returns>
    Task<ICollection<string>> GetLikedActivitiesAsync(string username, int skip, int limit);

    Task<bool> IsLikedByActorAsync(string username, string targetActivityId);

    Task<string?> GetLikeByActorAsync(string username, string targetActivityId);

    Task<bool> IsBoostedByActorAsync(string username, string targetActivityId);

    Task<string?> GetBoostByActorAsync(string username, string targetActivityId);

    Task<int> GetLikeCountAsync(string activityId);

    Task<int> GetBoostCountAsync(string activityId);

    Task<int> GetReplyCountAsync(string activityId);

    /// <summary>
    /// Gets the follower count for an actor
    /// </summary>
    Task<int> GetFollowerCountAsync(string username);

    /// <summary>
    /// Gets the following count for an actor
    /// </summary>
    Task<int> GetFollowingCountAsync(string username);
}