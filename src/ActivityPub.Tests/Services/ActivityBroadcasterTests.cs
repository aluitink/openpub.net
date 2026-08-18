using ActivityPub.WebUI.Services;
using Xunit;

namespace ActivityPub.Tests.Services;

public class ActivityBroadcasterTests
{
    [Fact]
    public async Task Publish_DeliversToSubscribers()
    {
        var broadcaster = new ActivityBroadcaster();
        var reader = broadcaster.Subscribe();

        broadcaster.Publish(new NewActivityEvent("act-1", "Note", "alice", "hello", "2026-01-01T00:00:00Z"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var evt = await reader.ReadAsync(cts.Token);

        Assert.Equal("act-1", evt.ActivityId);
        Assert.Equal("Note", evt.Type);
        Assert.Equal("alice", evt.ActorName);
        Assert.Equal("hello", evt.Content);
        Assert.Equal("2026-01-01T00:00:00Z", evt.Timestamp);
    }

    [Fact]
    public async Task Publish_FansOutToAllSubscribers()
    {
        var broadcaster = new ActivityBroadcaster();
        var readerA = broadcaster.Subscribe();
        var readerB = broadcaster.Subscribe();

        broadcaster.Publish(new NewActivityEvent("act-2", "Note", "bob", "hi", "2026-01-02T00:00:00Z"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var a = await readerA.ReadAsync(cts.Token);
        var b = await readerB.ReadAsync(cts.Token);

        Assert.Equal("act-2", a.ActivityId);
        Assert.Equal("act-2", b.ActivityId);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        var broadcaster = new ActivityBroadcaster();
        var reader = broadcaster.Subscribe();
        broadcaster.Unsubscribe(reader);

        broadcaster.Publish(new NewActivityEvent("act-3", "Note", "carol", "x", "2026-01-03T00:00:00Z"));

        Assert.False(reader.TryRead(out _), "unsubscribed reader must not receive events");
    }

    [Fact]
    public async Task Publish_WithoutSubscribers_DoesNotThrow()
    {
        var broadcaster = new ActivityBroadcaster();
        var ex = Record.Exception(() =>
            broadcaster.Publish(new NewActivityEvent("act-4", "Note", "dave", "y", "2026-01-04T00:00:00Z")));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Publish_DoesNotBlockWhenSubscriberIsFull()
    {
        var broadcaster = new ActivityBroadcaster();
        var reader = broadcaster.Subscribe(bufferSize: 4);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        // Well over the buffer: with DropOldest the publisher must never stall.
        for (int i = 0; i < 1000; i++)
            broadcaster.Publish(new NewActivityEvent("act-" + i, "Note", "eve", "z", "2026-01-05T00:00:00Z"));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
            $"Publish with a full, unread subscriber took {sw.Elapsed}; it must not block");

        // With DropOldest the oldest events are discarded under back-pressure.
        // Whatever survives must be a *recent* event (not one of the first few
        // that were dropped), proving the publisher never stalled. The exact
        // surviving set depends on drop timing, so assert on the recency
        // window rather than a specific id.
        Assert.True(reader.TryRead(out var first), "expected a readable event");
        var firstIndex = int.Parse(first.ActivityId["act-".Length..]);
        Assert.True(firstIndex > 900,
            $"expected a recent event to survive, got {first.ActivityId} (index {firstIndex}); oldest were dropped");
    }
}
