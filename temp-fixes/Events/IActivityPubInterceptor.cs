using ActivityPub.Core.Models;

namespace ActivityPub.Core.Events;

/// <summary>
/// Interface for intercepting ActivityPub events for custom processing
/// </summary>
public interface IActivityPubInterceptor
{
    /// <summary>
    /// Called when an activity is received
    /// </summary>
    /// <param name="activity">The received activity</param>
    /// <returns>True if the activity should continue processing, false to cancel</returns>
    Task<bool> OnActivityReceivedAsync(Activity activity);
    
    /// <summary>
    /// Called when an activity is published
    /// </summary>
    /// <param name="activity">The published activity</param>
    /// <returns>True if the activity should continue publishing, false to cancel</returns>
    Task<bool> OnActivityPublishedAsync(Activity activity);
    
    /// <summary>
    /// Called when a follow activity is received
    /// </summary>
    /// <param name="activity">The received follow activity</param>
    /// <returns>True if the follow should continue processing, false to cancel</returns>
    Task<bool> OnFollowReceivedAsync(Activity activity);
    
    /// <summary>
    /// Called when a post (note) is created
    /// </summary>
    /// <param name="note">The created note</param>
    /// <returns>True if the note should continue processing, false to cancel</returns>
    Task<bool> OnPostCreatedAsync(Note note);
}