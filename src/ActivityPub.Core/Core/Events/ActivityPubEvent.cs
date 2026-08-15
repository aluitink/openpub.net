using ActivityPub.Core.Models;
using System;

namespace ActivityPub.Core.Events;

/// <summary>
/// Base class for all ActivityPub events
/// </summary>
public abstract class ActivityPubEvent : IActivityPubEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string EventType { get; init; } = string.Empty;
}

/// <summary>
/// Event raised when an activity is received
/// </summary>
public class ActivityReceivedEvent : ActivityPubEvent, IActivityReceivedEvent
{
    public Activity Activity { get; init; }

    public ActivityReceivedEvent(Activity activity)
    {
        Activity = activity;
        EventType = "ActivityReceived";
    }
}

/// <summary>
/// Event raised when an activity is published
/// </summary>
public class ActivityPublishedEvent : ActivityPubEvent, IActivityPublishedEvent
{
    public Activity Activity { get; init; }

    public ActivityPublishedEvent(Activity activity)
    {
        Activity = activity;
        EventType = "ActivityPublished";
    }
}

/// <summary>
/// Event raised when a follow activity is received
/// </summary>
public class FollowReceivedEvent : ActivityPubEvent, IFollowReceivedEvent
{
    public Activity Activity { get; init; }

    public FollowReceivedEvent(Activity activity)
    {
        Activity = activity;
        EventType = "FollowReceived";
    }
}

/// <summary>
/// Event raised when a post (note) is created
/// </summary>
public class PostCreatedEvent : ActivityPubEvent, IPostCreatedEvent
{
    public Note Note { get; init; }

    public PostCreatedEvent(Note note)
    {
        Note = note;
        EventType = "PostCreated";
    }
}