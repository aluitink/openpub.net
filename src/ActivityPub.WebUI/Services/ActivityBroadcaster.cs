using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ActivityPub.WebUI.Services;

/// <summary>
/// A single new-activity event as broadcast to live listeners (SSE + SignalR).
/// Mirrors the payload sent by <see cref="INotificationService.BroadcastNewActivityAsync"/>.
/// </summary>
public sealed record NewActivityEvent(string ActivityId, string Type, string ActorName, string Content, string Timestamp);

/// <summary>
/// In-process pub/sub for new-activity events. Each subscriber gets its own
/// bounded channel; <see cref="Publish"/> fans out to all of them without
/// blocking the composer. Used as the SSE fallback transport and as the
/// single source of truth that the SignalR hub also mirrors.
/// </summary>
public interface IActivityBroadcaster
{
    void Publish(NewActivityEvent evt);
    ChannelReader<NewActivityEvent> Subscribe(int bufferSize = 128);
    void Unsubscribe(ChannelReader<NewActivityEvent> reader);
}

public sealed class ActivityBroadcaster : IActivityBroadcaster
{
    private readonly ConcurrentDictionary<ChannelReader<NewActivityEvent>, ChannelWriter<NewActivityEvent>> _subscribers
        = new();

    public void Publish(NewActivityEvent evt)
    {
        foreach (var writer in _subscribers.Values)
        {
            // TryWrite keeps the composer from stalling if a subscriber is slow;
            // a dropped event on a slow SSE client is acceptable for a fallback.
            writer.TryWrite(evt);
        }
    }

    public ChannelReader<NewActivityEvent> Subscribe(int bufferSize = 128)
    {
        var channel = Channel.CreateBounded<NewActivityEvent>(new BoundedChannelOptions(bufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _subscribers[channel.Reader] = channel.Writer;
        return channel.Reader;
    }

    public void Unsubscribe(ChannelReader<NewActivityEvent> reader)
    {
        _subscribers.TryRemove(reader, out _);
    }
}
