using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ActivityPub.WebUI.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public async Task JoinUserGroup()
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{username}");
        }
    }

    public async Task LeaveUserGroup()
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{username}");
        }
    }

    public async Task AcknowledgeNotification(string notificationId)
    {
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
        await base.OnDisconnectedAsync(exception);
    }
}
