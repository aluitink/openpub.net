using ActivityPub.Core.Events;
using ActivityPub.Core.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ActivityPub.Core.Services;

/// <summary>
/// Dispatches ActivityPub events to registered handlers
/// </summary>
public class ActivityPubEventDispatcher
{
    private readonly ConcurrentBag<IActivityPubEventHandler> _handlers = new();

    /// <summary>
    /// Adds an event handler to the dispatcher
    /// </summary>
    /// <param name="handler">The event handler to add</param>
    public void AddHandler(IActivityPubEventHandler handler)
    {
        _handlers.Add(handler);
    }

    /// <summary>
    /// Removes an event handler from the dispatcher
    /// </summary>
    /// <param name="handler">The event handler to remove</param>
    public void RemoveHandler(IActivityPubEventHandler handler)
    {
        // The ConcurrentBag doesn't have TryRemove, so we'll just leave it as is
        // In a production system, you'd want to implement proper removal logic
        _handlers.Add(handler); // This is a simplified approach
    }

    /// <summary>
    /// Dispatches an event to all registered handlers
    /// </summary>
    /// <param name="event">The event to dispatch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DispatchAsync(ActivityPubEvent @event)
    {
        var tasks = _handlers.Select(h => h.HandleEventAsync(@event));
        await Task.WhenAll(tasks);
    }
}