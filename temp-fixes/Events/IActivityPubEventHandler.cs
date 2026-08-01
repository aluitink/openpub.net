using System.Threading.Tasks;

namespace ActivityPub.Core.Events;

/// <summary>
/// Interface for ActivityPub event handlers
/// </summary>
public interface IActivityPubEventHandler
{
    /// <summary>
    /// Handles an ActivityPub event
    /// </Allowed>
    Task HandleEventAsync(ActivityPubEvent @event);
}