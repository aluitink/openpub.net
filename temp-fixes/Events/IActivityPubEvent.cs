using ActivityPub.Core.Models;
using System.Threading.Tasks;

namespace ActivityPub.Core.Events;

/// <summary>
/// Base event interface for ActivityPub events
/// </summary>
public interface IActivityPubEvent
{
    /// <summary>
    /// Gets the event timestamp
    /// </summary>
    DateTime Timestamp { get; }
    
    /// <summary>
    /// Gets the event type
    /// </summary>
    string EventType { get; }
}

/// <summary>
/// Event raised when an activity is received
/// </summary>
public interface IActivityReceivedEvent : IActivityPubEvent
{
    /// <summary>
    /// Gets the received activity
    /// </summary>
    Activity Activity { get; }
}

/// <summary>
/// Event raised when an activity is published
/// </summary>
public interface IActivityPublishedEvent : IActivityPubEvent
{
    /// <summary>
    /// Gets the published activity
    /// </summary>
    Activity Activity { get; }
}

/// <summary>
/// Event raised when a follow activity is received
/// </summary>
public interface IFollowReceivedEvent : IActivityPubEvent
{
    /// <summary>
    /// Gets the received follow activity
    /// </summary>
    Activity Activity { get; }
}

/// <summary>
/// Event raised when a post (note) is created
/// </summary>
public interface IPostCreatedEvent : IActivityPubEvent
{
    /// <summary>
    /// Gets the created note
    /// </summary>
    Note Note { get; }
}