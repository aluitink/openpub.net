using ActivityPub.Core.Models;

namespace ActivityPub.Core.Caching;

/// <summary>
/// Interface for federation caching operations
/// </summary>
public interface IFederationCache
{
    #region Actor Caching

    /// <summary>
    /// Gets an actor from cache by URI
    /// </summary>
    /// <param name="uri">The actor URI</param>
    /// <returns>The cached actor if found, null otherwise</returns>
    Task<Actor?> GetActorAsync(string uri);

    /// <summary>
    /// Sets an actor in cache
    /// </summary>
    /// <param name="uri">The actor URI</param>
    /// <param name="actor">The actor to cache</param>
    Task SetActorAsync(string uri, Actor actor);

    /// <summary>
    /// Removes an actor from cache
    /// </summary>
    /// <param name="uri">The actor URI</param>
    Task RemoveActorAsync(string uri);

    /// <summary>
    /// Invalidates all actor caches for a domain
    /// </summary>
    /// <param name="domain">The domain to invalidate</param>
    Task InvalidateActorsByDomainAsync(string domain);

    #endregion

    #region Activity Caching

    /// <summary>
    /// Gets an activity from cache by ID
    /// </summary>
    /// <param name="activityId">The activity ID</param>
    /// <returns>The cached activity if found, null otherwise</returns>
    Task<Activity?> GetActivityAsync(string activityId);

    /// <summary>
    /// Sets an activity in cache
    /// </summary>
    /// <param name="activityId">The activity ID</param>
    /// <param name="activity">The activity to cache</param>
    Task SetActivityAsync(string activityId, Activity activity);

    /// <summary>
    /// Removes an activity from cache
    /// </summary>
    /// <param name="activityId">The activity ID</param>
    Task RemoveActivityAsync(string activityId);

    /// <summary>
    /// Invalidates all activities by actor
    /// </summary>
    /// <param name="actorId">The actor ID</param>
    Task InvalidateActivitiesByActorAsync(string actorId);

    #endregion

    #region WebFinger Caching

    /// <summary>
    /// Gets a WebFinger response from cache
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <returns>The cached WebFinger response if found, null otherwise</returns>
    Task<WebFingerResponse?> GetWebFingerResponseAsync(string key);

    /// <summary>
    /// Sets a WebFinger response in cache
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="response">The WebFinger response to cache</param>
    Task SetWebFingerResponseAsync(string key, WebFingerResponse response);

    /// <summary>
    /// Removes a WebFinger response from cache
    /// </summary>
    /// <param name="key">The cache key</param>
    Task RemoveWebFingerResponseAsync(string key);

    /// <summary>
    /// Invalidates all WebFinger responses for a domain
    /// </summary>
    /// <param name="domain">The domain to invalidate</param>
    Task InvalidateWebFingerByDomainAsync(string domain);

    #endregion

    #region Inbox Response Caching

    /// <summary>
    /// Gets an inbox response from cache
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <returns>The cached response if found, null otherwise</returns>
    Task<string?> GetInboxResponseAsync(string key);

    /// <summary>
    /// Sets an inbox response in cache
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="response">The response to cache</param>
    Task SetInboxResponseAsync(string key, string response);

    /// <summary>
    /// Removes an inbox response from cache
    /// </summary>
    /// <param name="key">The cache key</param>
    Task RemoveInboxResponseAsync(string key);

    /// <summary>
    /// Invalidates all inbox responses for an actor
    /// </summary>
    /// <param name="actorId">The actor ID</param>
    Task InvalidateInboxResponsesByActorAsync(string actorId);

    #endregion

    #region Cache Management

    /// <summary>
    /// Clears all cached entries
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Gets the number of items in cache
    /// </summary>
    int Count { get; }

    #endregion
}
