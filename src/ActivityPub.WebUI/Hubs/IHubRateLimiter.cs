namespace ActivityPub.WebUI.Hubs;

/// <summary>
/// Tracks per-connection message counts for SignalR hub rate limiting. The
/// in-memory implementation is process-local; the Redis implementation shares
/// counts across all instances so that a connection's usage is enforced
/// regardless of which instance currently holds it.
/// </summary>
public interface IHubRateLimiter
{
    /// <summary>
    /// Records a message for the given connection and returns <see langword="true"/>
    /// if the connection is still within the allowed limit for the current window.
    /// </summary>
    /// <param name="connectionId">The SignalR connection identifier.</param>
    /// <param name="maxMessages">Maximum messages allowed within the window.</param>
    /// <param name="window">Length of the sliding window.</param>
    Task<bool> TryRecordAsync(string connectionId, int maxMessages, TimeSpan window);

    /// <summary>
    /// Clears any tracked state for the connection (e.g. on disconnect).
    /// </summary>
    Task ClearAsync(string connectionId);
}
