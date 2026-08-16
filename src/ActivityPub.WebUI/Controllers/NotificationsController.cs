using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Obj = ActivityPub.Core.Models.Object;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(IActivityPubRepository repository, ILogger<NotificationsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
            return NotFound();

        var pageSize = 20;
        var skip = (page - 1) * pageSize;

        var inboxActivityIds = await _repository.GetInboxActivitiesAsync(username, skip, pageSize);
        var items = new List<NotificationItem>();

        foreach (var activityId in inboxActivityIds)
        {
            var activity = await _repository.GetActivityAsync(activityId);
            if (activity == null) continue;

            var notificationType = ClassifyNotification(activity, actor);
            if (notificationType == null) continue;

            var (actorName, actorInfo) = await GetActorFromActivity(activity);
            if (actorInfo == null) continue;

            var displayName = actorInfo.Name ?? actorName;
            var item = new NotificationItem
            {
                ActivityId = activity.Id ?? "",
                NotificationType = notificationType,
                ActorName = actorName,
                ActorDisplayName = displayName,
                ActorUrl = actorInfo.Id ?? "",
                Published = activity.Published ?? DateTime.UtcNow,
                Title = GetNotificationTitle(notificationType, displayName),
                TargetNote = await GetTargetNoteContent(activity),
            };
            items.Add(item);
        }

        items.Sort((a, b) => b.Published.CompareTo(a.Published));

        var viewModel = new NotificationsViewModel
        {
            Items = items,
            Page = page,
            HasMore = items.Count == pageSize,
            TotalCount = items.Count,
        };

        return View(viewModel);
    }

    static string? ClassifyNotification(Activity activity, Actor targetActor)
    {
        var type = activity.Type;
        if (type == "Follow")
        {
            if (activity.ObjectId == targetActor.Id)
                return "follow";
        }
        else if (type == "Accept")
        {
            return "follow_accept";
        }
        else if (type == "Like")
        {
            return "like";
        }
        else if (type == "Announce")
        {
            return "boost";
        }
        else if (type == "Create")
        {
            if (activity.Object is Obj obj && !string.IsNullOrEmpty(obj.InReplyTo))
                return "reply";
        }

        return null;
    }

    async Task<(string Username, Actor? Actor)> GetActorFromActivity(Activity activity)
    {
        var actorUrl = activity.ActorId;
        if (string.IsNullOrEmpty(actorUrl) && activity.Actor != null)
            actorUrl = activity.Actor.ToString();
        if (string.IsNullOrEmpty(actorUrl)) return ("", null);

        var username = ExtractUsername(actorUrl);
        if (string.IsNullOrEmpty(username)) return ("", null);

        var actor = await _repository.GetUserActorAsync(username);
        return (username, actor);
    }

    async Task<string?> GetTargetNoteContent(Activity activity)
    {
        var objectId = activity.ObjectId;
        if (string.IsNullOrEmpty(objectId)) return null;

        var targetActivity = await _repository.GetActivityAsync(objectId);
        if (targetActivity != null && targetActivity.Object is Obj obj)
        {
            return Truncate(obj.Content ?? "", 100);
        }

        return null;
    }

    static string? ExtractUsername(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var parts = url.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "users" && i + 1 < parts.Length)
                return parts[i + 1];
        }
        return null;
    }

    static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    static string GetNotificationTitle(string type, string displayName)
    {
        return type switch
        {
            "follow" => $"{displayName} is now following you",
            "follow_accept" => $"{displayName} accepted your follow request",
            "like" => $"{displayName} liked your note",
            "boost" => $"{displayName} boosted your note",
            "reply" => $"{displayName} replied to your note",
            _ => $"{displayName} interacted with your content"
        };
    }
}

public class NotificationsViewModel
{
    public List<NotificationItem> Items { get; set; } = new();
    public int Page { get; set; }
    public bool HasMore { get; set; }
    public int TotalCount { get; set; }
}

public class NotificationItem
{
    public string ActivityId { get; set; } = "";
    public string NotificationType { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string ActorDisplayName { get; set; } = "";
    public string ActorUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public string? TargetNote { get; set; }
    public DateTime Published { get; set; }
}