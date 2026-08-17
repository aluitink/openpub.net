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
    /// Returns every stored activity ID (its ActivityPub URL). Used by the REST
    /// API to translate numeric status IDs back to their canonical URL.
    /// </summary>
    Task<ICollection<string>> GetAllActivityIdsAsync();

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
    /// Gets pending shared inbox deliveries that are eligible for an attempt
    /// now. A <c>Failed</c> delivery is only included when it has not exceeded
    /// <paramref name="maxRetries"/> and its <c>NextRetryAt</c> backoff gate has
    /// passed.
    /// </summary>
    /// <param name="maxCount">Maximum number of deliveries to retrieve</param>
    /// <param name="maxRetries">Retry cap used to gate <c>Failed</c> deliveries</param>
    /// <returns>Collection of pending deliveries</returns>
    Task<ICollection<SharedInboxDeliveryEntity>> GetPendingSharedInboxDeliveriesAsync(int maxCount = 100, int maxRetries = 5);

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
    /// Adds an inbound activity to the dead-letter queue, or returns the
    /// existing dead-lettered row for the same activity + inbox when one is
    /// already there (a redelivery of the same failing activity updates the
    /// row instead of creating a duplicate).
    /// </summary>
    /// <param name="entity">The dead-letter row to store</param>
    /// <returns>The stored row (the new one, or the existing one for that activity)</returns>
    Task<InboxDeadLetterEntity> AddInboxDeadLetterAsync(InboxDeadLetterEntity entity);

    /// <summary>
    /// Gets dead-lettered inbound activities, optionally filtered to a single
    /// activity + inbox, ordered oldest first.
    /// </summary>
    /// <param name="maxCount">Maximum number of rows to retrieve</param>
    /// <param name="activityId">Optional activity ID filter</param>
    /// <param name="username">Optional inbox username filter (used together with <paramref name="activityId"/>)</param>
    /// <returns>Collection of dead-letter rows</returns>
    Task<ICollection<InboxDeadLetterEntity>> GetInboxDeadLettersAsync(int maxCount = 100, string? activityId = null, string? username = null);

    /// <summary>
    /// Gets dead-letter rows eligible for re-processing now: status
    /// <c>DeadLettered</c> and either never re-processed or past their backoff
    /// window (<c>NextRetryAt</c> is stored on the row via
    /// <see cref="UpdateInboxDeadLetterAsync"/>).
    /// </summary>
    /// <param name="maxCount">Maximum number of rows to retrieve</param>
    /// <returns>Collection of re-processable dead-letter rows</returns>
    Task<ICollection<InboxDeadLetterEntity>> GetReprocessableInboxDeadLettersAsync(int maxCount = 100);

    /// <summary>
    /// Updates a dead-letter row (status, attempt count, failure reason, ...).
    /// </summary>
    /// <param name="entity">The dead-letter row to update</param>
    /// <returns>True if updated successfully, false otherwise</returns>
    Task<bool> UpdateInboxDeadLetterAsync(InboxDeadLetterEntity entity);

    /// <summary>
    /// Deletes dead-letter rows older than <paramref name="cutoff"/> (retention
    /// pruning by the background service).
    /// </summary>
    /// <param name="cutoff">Rows with <c>CreatedAt</c> before this time are removed</param>
    /// <returns>Number of rows removed</returns>
    Task<int> PruneInboxDeadLettersAsync(DateTime cutoff);

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

    /// <summary>
    /// Gets the number of notes (Create activities of type Note) authored by an actor.
    /// </summary>
    Task<int> GetNoteCountAsync(string username);

    /// <summary>
    /// Gets whether one actor currently follows another (an active Follow, not undone).
    /// </summary>
    Task<bool> IsFollowingAsync(string followerUsername, string targetActorId);

    /// <summary>
    /// Gets the federation peer health record for a domain, or null if the
    /// domain has not been seen yet.
    /// </summary>
    Task<FederationPeerEntity?> GetFederationPeerAsync(string domain);

    /// <summary>
    /// Saves (inserts or updates) a federation peer health record, keyed by
    /// domain.
    /// </summary>
    Task<bool> SaveFederationPeerAsync(FederationPeerEntity peer);

    /// <summary>
    /// Gets all federation peer health records, optionally filtered to only
    /// blocked peers.
    /// </summary>
    Task<ICollection<FederationPeerEntity>> GetFederationPeersAsync(bool onlyBlocked = false);

    /// <summary>
    /// Gets all domains that are currently blocked, for use in the inbound
    /// rejection path (a snapshot to avoid a DB hit per activity).
    /// </summary>
    Task<ICollection<string>> GetBlockedDomainNamesAsync();
}