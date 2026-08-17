using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ActivityPub.Core.Options;

namespace ActivityPub.WebUI.Hubs;

public class NotificationHub : Hub
{
    private readonly IHubRateLimiter _rateLimiter;
    private readonly RealtimeOptions _options;

    public NotificationHub(IHubRateLimiter rateLimiter, IOptions<ActivityPubOptions> options)
    {
        _rateLimiter = rateLimiter;
        _options = options.Value.Realtime;
    }

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
        var allowed = await _rateLimiter.TryRecordAsync(
            Context.ConnectionId,
            _options.MaxMessagesPerWindow,
            _options.Window);

        if (!allowed)
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
        await _rateLimiter.ClearAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
