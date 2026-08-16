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

    public SignalRNotificationService(IHubContext<ActivityPub.WebUI.Hubs.NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastNewActivityAsync(string activityId, string type, string actorName, string content)
    {
        await _hubContext.Clients.All.SendAsync("NewActivity", new
        {
            ActivityId = activityId,
            Type = type,
            ActorName = actorName,
            Content = content,
            Timestamp = DateTime.UtcNow.ToString("O")
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
