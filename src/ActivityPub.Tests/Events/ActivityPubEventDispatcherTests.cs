using ActivityPub.Core.Events;
using ActivityPub.Core.Models;
using Xunit;

namespace ActivityPub.Tests.Events;

public class ActivityPubEventDispatcherTests
{
    private readonly ActivityPubEventDispatcher _dispatcher;

    public ActivityPubEventDispatcherTests()
    {
        _dispatcher = new ActivityPubEventDispatcher();
    }

    [Fact]
    public async Task Subscribe_AddsHandlerForEventType()
    {
        var handlerInvoked = false;
        Func<IActivityPubEvent, Task> handler = _ =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        };

        _dispatcher.Subscribe("test.event", handler);

        var @event = new TestEvent("test.event");
        await _dispatcher.DispatchAsync(@event);

        Assert.True(handlerInvoked);
    }

    [Fact]
    public async Task Unsubscribe_RemovesHandler()
    {
        var handlerInvoked = false;
        Func<IActivityPubEvent, Task> handler = _ =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        };

        _dispatcher.Subscribe("test.event", handler);
        _dispatcher.Unsubscribe("test.event", handler);

        var @event = new TestEvent("test.event");
        await _dispatcher.DispatchAsync(@event);

        Assert.False(handlerInvoked);
    }

    [Fact]
    public async Task DispatchAsync_MultipleHandlersAllInvoked()
    {
        var handler1Invoked = false;
        var handler2Invoked = false;

        Func<IActivityPubEvent, Task> handler1 = _ =>
        {
            handler1Invoked = true;
            return Task.CompletedTask;
        };

        Func<IActivityPubEvent, Task> handler2 = _ =>
        {
            handler2Invoked = true;
            return Task.CompletedTask;
        };

        _dispatcher.Subscribe("test.event", handler1);
        _dispatcher.Subscribe("test.event", handler2);

        var @event = new TestEvent("test.event");
        await _dispatcher.DispatchAsync(@event);

        Assert.True(handler1Invoked);
        Assert.True(handler2Invoked);
    }

    [Fact]
    public async Task DispatchAsync_DifferentEventTypes_Isolated()
    {
        var event1HandlerInvoked = false;
        var event2HandlerInvoked = false;

        Func<IActivityPubEvent, Task> event1Handler = _ =>
        {
            event1HandlerInvoked = true;
            return Task.CompletedTask;
        };

        Func<IActivityPubEvent, Task> event2Handler = _ =>
        {
            event2HandlerInvoked = true;
            return Task.CompletedTask;
        };

        _dispatcher.Subscribe("event1", event1Handler);
        _dispatcher.Subscribe("event2", event2Handler);

        var event1 = new TestEvent("event1");
        await _dispatcher.DispatchAsync(event1);

        Assert.True(event1HandlerInvoked);
        Assert.False(event2HandlerInvoked);

        event1HandlerInvoked = false;
        var event2 = new TestEvent("event2");
        await _dispatcher.DispatchAsync(event2);

        Assert.False(event1HandlerInvoked);
        Assert.True(event2HandlerInvoked);
    }

    [Fact]
    public async Task DispatchAsync_NoSubscribers_DoesNotThrow()
    {
        var @event = new TestEvent("nonexistent.event");
        await _dispatcher.DispatchAsync(@event);
    }

    // --- Runtime dispatcher (Core.Services) regression tests -------------
    // The dispatcher actually wired into the runtime path (ActivityPubService /
    // InboxProcessorService / DI) is Core.Services.ActivityPubEventDispatcher.
    // These tests pin the Add/Remove/Dispatch contract for that class.

    [Fact]
    public async Task ServicesDispatcher_AddHandler_ThenDispatch_InvokesHandler()
    {
        var dispatcher = new ActivityPub.Core.Services.ActivityPubEventDispatcher();
        var invocations = 0;
        var handler = new CountingEventHandler(() => invocations++);

        dispatcher.AddHandler(handler);
        await dispatcher.DispatchAsync(new TestEvent("test.event"));

        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task ServicesDispatcher_RemoveHandler_StopsInvocation()
    {
        var dispatcher = new ActivityPub.Core.Services.ActivityPubEventDispatcher();
        var invocations = 0;
        var handler = new CountingEventHandler(() => invocations++);

        dispatcher.AddHandler(handler);
        dispatcher.RemoveHandler(handler);
        await dispatcher.DispatchAsync(new TestEvent("test.event"));

        // After removal the handler must NOT be invoked. (The previous
        // implementation *added* the handler on RemoveHandler, so it would have
        // been invoked — twice.)
        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task ServicesDispatcher_RemoveHandler_LeavesOtherHandlersIntact()
    {
        var dispatcher = new ActivityPub.Core.Services.ActivityPubEventDispatcher();
        var handler1Invocations = 0;
        var handler2Invocations = 0;
        var handler1 = new CountingEventHandler(() => handler1Invocations++);
        var handler2 = new CountingEventHandler(() => handler2Invocations++);

        dispatcher.AddHandler(handler1);
        dispatcher.AddHandler(handler2);
        dispatcher.RemoveHandler(handler1);
        await dispatcher.DispatchAsync(new TestEvent("test.event"));

        Assert.Equal(0, handler1Invocations);
        Assert.Equal(1, handler2Invocations);
    }

    [Fact]
    public async Task ServicesDispatcher_MultipleHandlers_AllInvoked()
    {
        var dispatcher = new ActivityPub.Core.Services.ActivityPubEventDispatcher();
        var handler1Invocations = 0;
        var handler2Invocations = 0;
        var handler1 = new CountingEventHandler(() => handler1Invocations++);
        var handler2 = new CountingEventHandler(() => handler2Invocations++);

        dispatcher.AddHandler(handler1);
        dispatcher.AddHandler(handler2);
        await dispatcher.DispatchAsync(new TestEvent("test.event"));

        Assert.Equal(1, handler1Invocations);
        Assert.Equal(1, handler2Invocations);
    }

    private class TestEvent : ActivityPubEvent
    {
        public TestEvent(string eventType)
        {
            EventType = eventType;
        }
    }

    private sealed class CountingEventHandler : IActivityPubEventHandler
    {
        private readonly Action _onEvent;
        public CountingEventHandler(Action onEvent) => _onEvent = onEvent;
        public Task HandleEventAsync(ActivityPubEvent @event)
        {
            _onEvent();
            return Task.CompletedTask;
        }
    }
}
