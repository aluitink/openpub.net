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
    // A ConcurrentDictionary keyed by handler identity gives us thread-safe add
    // AND removal (ConcurrentBag has no TryRemove). Keying on the handler
    // instance means re-adding the same handler is idempotent and removing it
    // actually removes it.
    private readonly ConcurrentDictionary<IActivityPubEventHandler, byte> _handlers = new();

    /// <summary>
    /// Adds an event handler to the dispatcher
    /// </summary>
    /// <param name="handler">The event handler to add</param>
    public void AddHandler(IActivityPubEventHandler handler)
    {
        _handlers.TryAdd(handler, 0);
    }

    /// <summary>
    /// Removes an event handler from the dispatcher
    /// </summary>
    /// <param name="handler">The event handler to remove</param>
    public void RemoveHandler(IActivityPubEventHandler handler)
    {
        _handlers.TryRemove(handler, out _);
    }

    /// <summary>
    /// Dispatches an event to all registered handlers
    /// </summary>
    /// <param name="event">The event to dispatch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DispatchAsync(ActivityPubEvent @event)
    {
        // Snapshot the keys so a concurrent add/remove during dispatch doesn't
        // throw a ConcurrentDictionary enumeration exception.
        var handlers = _handlers.Keys.ToArray();
        var tasks = handlers.Select(h => h.HandleEventAsync(@event));
        await Task.WhenAll(tasks);
    }
}