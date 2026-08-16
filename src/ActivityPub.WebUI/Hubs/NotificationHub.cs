using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ActivityPub.WebUI.Hubs;

public class NotificationHub : Hub
{
    private static readonly ConcurrentDictionary<string, HubRateLimitState> _rateLimits = new();
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(1);
    private const int _maxMessages = 50;

    [Authorize]
    public async Task JoinUserGroup()
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "user-" + username);
        }
    }

    [Authorize]
    public async Task LeaveUserGroup()
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "user-" + username);
        }
    }

    [Authorize]
    public async Task AcknowledgeNotification(string notificationId)
    {
        if (!CheckRateLimit())
        {
            await Clients.Caller.SendAsync("RateLimited", "Too many messages. Please wait.");
            return;
        }
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await Clients.Caller.SendAsync("NotificationAcknowledged", notificationId);
        }
    }

    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await JoinUserGroup();
        }
        await Clients.Caller.SendAsync("Connected", username);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await LeaveUserGroup();
        }
        _rateLimits.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

    private bool CheckRateLimit()
    {
        var connectionId = Context.ConnectionId;
        var state = _rateLimits.GetOrAdd(connectionId, _ => new HubRateLimitState());
        var now = DateTime.UtcNow;

        lock (state)
        {
            if (now - state.WindowStart > _window)
            {
                state.MessageCount = 0;
                state.WindowStart = now;
            }
            state.MessageCount++;
            return state.MessageCount <= _maxMessages;
        }
    }
}

internal class HubRateLimitState
{
    public DateTime WindowStart { get; set; } = DateTime.MinValue;
    public int MessageCount { get; set; }
}
