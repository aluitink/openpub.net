using ActivityPub.Core.Models;
using System.Collections.Concurrent;

namespace ActivityPub.Core.Events;

/// <summary>
/// Event dispatcher for ActivityPub events
/// </summary>
public class ActivityPubEventDispatcher
{
    private readonly ConcurrentDictionary<string, List<Func<IActivityPubEvent, Task>>> _handlers = new();

    /// <summary>
    /// Subscribes to an event type
    /// </summary>
    /// <param name="eventType">The event type to subscribe to</param>
    /// <param name="handler">The handler function</param>
    public void Subscribe(string eventType, Func<IActivityPubEvent, Task> handler)
    {
        var handlers = _handlers.GetOrAdd(eventType, _ => new List<Func<IActivityPubEvent, Task>>());
        handlers.Add(handler);
    }

    /// <summary>
    /// Unsubscribes from an event type
    /// </summary>
    /// <param name="eventType">The event type to unsubscribe from</param>
    /// <param name="handler">The handler function to remove</param>
    public void Unsubscribe(string eventType, Func<IActivityPubEvent, Task> handler)
    {
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            handlers.RemoveAll(h => h == handler);
        }
    }

    /// <summary>
    /// Dispatches an event to all subscribed handlers
    /// </summary>
    /// <param name="event">The event to dispatch</param>
    public async Task DispatchAsync(IActivityPubEvent @event)
    {
        if (_handlers.TryGetValue(@event.EventType, out var handlers))
        {
            foreach (var handler in handlers)
            {
                await handler(@event);
            }
        }
    }
}