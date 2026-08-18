using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using ActivityPub.WebUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class FollowController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly IWebFingerService _webFinger;
    private readonly ILogger<FollowController> _logger;
    private readonly INotificationService _notifications;

    public FollowController(
        IActivityPubRepository repository,
        IWebFingerService webFinger,
        ILogger<FollowController> logger,
        INotificationService notifications)
    {
        _repository = repository;
        _webFinger = webFinger;
        _logger = logger;
        _notifications = notifications;
    }

    [HttpGet]
    public IActionResult Index(string? handle)
    {
        var model = new FollowModel { Handle = handle, Error = null };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Follow(FollowModel model)
    {
        var username = User.Identity!.Name!;
        var handle = model.Handle?.Trim();

        if (string.IsNullOrWhiteSpace(handle))
        {
            ModelState.AddModelError("Handle", "Enter a handle to follow (e.g. username@example.com or just username for local users).");
            return View("Index", model);
        }

        var (targetActor, isLocal) = await ResolveActor(username, handle);
        if (targetActor == null)
        {
            model.Error = $"Could not find actor '{handle}'. Make sure the handle is correct.";
            return View("Index", model);
        }

        if (targetActor.Id == (await _repository.GetUserActorAsync(username))?.Id)
        {
            model.Error = "You cannot follow yourself.";
            return View("Index", model);
        }

        var existingFollows = await _repository.GetFollowingAsync(username, 0, 100);
        if (targetActor.Id != null && existingFollows.Contains(targetActor.Id))
        {
            model.Error = $"You are already following {handle}.";
            return View("Index", model);
        }

        var localActor = await _repository.GetUserActorAsync(username);
        if (localActor == null)
        {
            ModelState.AddModelError("", "Federation account not found.");
            return View("Index", model);
        }

        var now = DateTime.UtcNow;
        var followId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var followActivity = new Follow
        {
            Id = followId,
            Type = "Follow",
            Actor = localActor.Id,
            Object = targetActor.Id,
            Published = now,
            To = new List<string> { targetActor.Id ?? string.Empty }
        };

        await _repository.SaveActivityAsync(followActivity);
        _logger.LogInformation("User {Username} followed {TargetActorId} ({Handle})", username, targetActor.Id, handle);

        if (!isLocal)
        {
            await TrySendFollowActivityAsync(followActivity, localActor, targetActor);
        }
        else
        {
            // Real-time notification to the followed local user.
            var targetUsername = ExtractUsername(targetActor.Id);
            if (!string.IsNullOrEmpty(targetUsername) && targetUsername != username)
            {
                await _notifications.BroadcastNotificationAsync(
                    targetUsername, "follow", $"{localActor.PreferredUsername ?? username} is now following you");
            }
        }

        TempData["FollowSuccess"] = $"Now following {handle}!";
        return RedirectToAction("Index", "Timeline");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unfollow(string? actorId)
    {
        var username = User.Identity!.Name!;
        var localActor = await _repository.GetUserActorAsync(username);
        if (localActor == null)
        {
            return RedirectToAction("Following");
        }

        var followingActorIds = await _repository.GetFollowingAsync(username, 0, 100);
        if (actorId == null || !followingActorIds.Contains(actorId))
        {
            return RedirectToAction("Following");
        }

        string? followActivityId = null;
        var outboxActivities = await _repository.GetActorOutboxActivitiesAsync(username, 0, 100);
        foreach (var outboxId in outboxActivities)
        {
            var activity = await _repository.GetActivityAsync(outboxId);
            if (activity?.Type == "Follow" && activity.ObjectId == actorId)
            {
                followActivityId = outboxId;
                break;
            }
        }

        if (followActivityId == null)
        {
            return RedirectToAction("Following");
        }

        var now = DateTime.UtcNow;
        var undoId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var undoActivity = new Activity
        {
            Id = undoId,
            Type = "Undo",
            Actor = localActor.Id,
            Object = new Activity
            {
                Id = followActivityId,
                Type = "Follow",
                Actor = localActor.Id,
                Object = actorId,
                Published = now
            },
            Published = now,
            To = new List<string> { actorId }
        };

        await _repository.SaveActivityAsync(undoActivity);
        await _repository.DeleteActivityAsync(followActivityId);

        _logger.LogInformation("User {Username} unfollowed {ActorId}", username, actorId);
        TempData["UnfollowSuccess"] = true;
        return RedirectToAction("Following");
    }

    [HttpGet]
    public async Task<IActionResult> Following()
    {
        var username = User.Identity!.Name!;
        var followingIds = await _repository.GetFollowingAsync(username, 0, 100);

        var items = new List<FollowingItem>();
        foreach (var followedActorId in followingIds)
        {
            var actor = await TryGetActorFromId(followedActorId);
            if (actor != null && actor.Id != null)
            {
                var actorId = actor.Id;
                items.Add(new FollowingItem
                {
                    ActorId = actorId,
                    DisplayName = actor.Name ?? ExtractUsername(actorId) ?? "",
                    Username = actor.PreferredUsername ?? ExtractUsername(actorId) ?? "",
                    Inbox = actor.Inbox,
                    Domain = ExtractDomain(actorId)
                });
            }
        }

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Followers()
    {
        var username = User.Identity!.Name!;
        var followerIds = await _repository.GetFollowersAsync(username, 0, 100);

        var items = new List<FollowingItem>();
        foreach (var followerActorId in followerIds)
        {
            var actor = await TryGetActorFromId(followerActorId);
            if (actor != null && actor.Id != null)
            {
                var actorId = actor.Id;
                items.Add(new FollowingItem
                {
                    ActorId = actorId,
                    DisplayName = actor.Name ?? ExtractUsername(actorId) ?? "",
                    Username = actor.PreferredUsername ?? ExtractUsername(actorId) ?? "",
                    Inbox = actor.Inbox,
                    Domain = ExtractDomain(actorId)
                });
            }
        }

        return View(items);
    }

    private async Task<(Actor? Actor, bool IsLocal)> ResolveActor(string currentUsername, string handle)
    {
        var parts = handle.Split('@');

        if (parts.Length == 1)
        {
            var localActor = await _repository.GetUserActorAsync(handle);
            return (localActor, true);
        }

        if (parts.Length == 2)
        {
            if (parts[1] == "localhost" || parts[1].Contains("localhost"))
            {
                var localActor = await _repository.GetUserActorAsync(parts[0]);
                if (localActor != null)
                    return (localActor, true);
            }

            var remoteActor = await _webFinger.ResolveActorAsync(handle);
            if (remoteActor != null)
                return (remoteActor, false);

            return (null, false);
        }

        return (null, false);
    }

    private async Task TrySendFollowActivityAsync(Follow followActivity, Actor localActor, Actor targetActor)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            var activityJson = JsonSerializer.Serialize(followActivity, jsonOptions);

            if (targetActor.SharedInbox != null)
            {
                await _repository.QueueSharedInboxDeliveryAsync(followActivity.Id!, activityJson, targetActor.SharedInbox);
            }
            else if (targetActor.Inbox != null)
            {
                await _repository.QueueSharedInboxDeliveryAsync(followActivity.Id!, activityJson, targetActor.Inbox);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue follow activity for delivery to {TargetActorId}", targetActor.Id);
        }
    }

    private async Task<Actor?> TryGetActorFromId(string actorId)
    {
        if (actorId.StartsWith("https://localhost"))
        {
            var username = ExtractUsername(actorId);
            if (username == null) return null;
            return await _repository.GetUserActorAsync(username);
        }
        return null;
    }

    private static string? ExtractUsername(string? actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return null;
        var parts = actorId.Split('/');
        if (parts.Length >= 2)
            return parts[parts.Length - 1];
        return actorId;
    }

    private static string? ExtractDomain(string? actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            return null;
        try
        {
            var uri = new Uri(actorId);
            return uri.Host;
        }
        catch
        {
            return null;
        }
    }

}

public class FollowModel
{
    public string? Handle { get; set; }
    public string? Error { get; set; }
}

public class FollowingItem
{
    public string ActorId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Username { get; set; } = "";
    public string? Inbox { get; set; }
    public string? Domain { get; set; }
}
