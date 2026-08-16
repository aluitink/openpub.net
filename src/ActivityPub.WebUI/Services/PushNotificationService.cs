using System.Collections.Concurrent;

namespace ActivityPub.WebUI.Services;

public interface IPushNotificationService
{
    Task RegisterSubscriptionAsync(string username, string endpoint, string p256dh, string auth);
    Task SendPushNotificationAsync(string username, string title, string body, string? icon = null);
}

public class PushNotificationService : IPushNotificationService
{
    private readonly ConcurrentDictionary<string, PushSubscription> _subscriptions = new();

    public async Task RegisterSubscriptionAsync(string username, string endpoint, string p256dh, string auth)
    {
        _subscriptions[username] = new PushSubscription
        {
            Username = username,
            Endpoint = endpoint,
            P256dh = p256dh,
            Auth = auth,
            RegisteredAt = DateTime.UtcNow
        };
        await Task.CompletedTask;
    }

    public async Task SendPushNotificationAsync(string username, string title, string body, string? icon)
    {
        if (_subscriptions.TryGetValue(username, out var subscription))
        {
            Console.WriteLine($"[Push] To {username}: {title} - {body} (endpoint: {subscription.Endpoint})");
        }
        else
        {
            Console.WriteLine($"[Push] No subscription for {username}: {title} - {body}");
        }
        await Task.CompletedTask;
    }
}

public class PushSubscription
{
    public string Username { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime RegisteredAt { get; set; }
}
