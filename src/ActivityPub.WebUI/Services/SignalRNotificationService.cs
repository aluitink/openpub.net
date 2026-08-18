using Microsoft.AspNetCore.SignalR;

namespace ActivityPub.WebUI.Services;

public interface INotificationService
{
    Task BroadcastNewActivityAsync(string activityId, string type, string actorName, string content);
    Task BroadcastNotificationAsync(string username, string notificationType, string message);
}

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<ActivityPub.WebUI.Hubs.NotificationHub> _hubContext;
    private readonly IActivityBroadcaster _broadcaster;

    public SignalRNotificationService(
        IHubContext<ActivityPub.WebUI.Hubs.NotificationHub> hubContext,
        IActivityBroadcaster broadcaster)
    {
        _hubContext = hubContext;
        _broadcaster = broadcaster;
    }

    public async Task BroadcastNewActivityAsync(string activityId, string type, string actorName, string content)
    {
        var timestamp = DateTime.UtcNow.ToString("O");

        // Fan out to SSE fallback subscribers first (fire-and-forget, never
        // blocks the composer), then to all connected SignalR clients.
        _broadcaster.Publish(new NewActivityEvent(activityId, type, actorName, content, timestamp));

        await _hubContext.Clients.All.SendAsync("NewActivity", new
        {
            ActivityId = activityId,
            Type = type,
            ActorName = actorName,
            Content = content,
            Timestamp = timestamp
        });
    }

    public async Task BroadcastNotificationAsync(string username, string notificationType, string message)
    {
        await _hubContext.Clients.Group($"user-{username}").SendAsync("NewNotification", new
        {
            Type = notificationType,
            Message = message,
            Timestamp = DateTime.UtcNow.ToString("O")
        });
    }
}
