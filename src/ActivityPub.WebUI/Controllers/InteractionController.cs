using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class InteractionController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<InteractionController> _logger;

    public InteractionController(IActivityPubRepository repository, ILogger<InteractionController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Like(string targetActivityId)
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return RedirectToAction("Index", "Timeline");
        }

        var targetActivity = await _repository.GetActivityAsync(targetActivityId);
        if (targetActivity == null)
        {
            _logger.LogWarning("Cannot like activity {ActivityId}: not found", targetActivityId);
            return RedirectToAction("Index", "Timeline");
        }

        if (await _repository.IsLikedByActorAsync(username, targetActivityId))
        {
            TempData["InteractionError"] = "You have already liked this post.";
            return RedirectToAction("Index", "Timeline");
        }

        var now = DateTime.UtcNow;
        var likeId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var likeActivity = new Activity
        {
            Id = likeId,
            Type = "Like",
            Actor = actor.Id,
            Object = targetActivityId,
            Published = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        await _repository.SaveActivityAsync(likeActivity);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var likeJson = JsonSerializer.Serialize(likeActivity, jsonOptions);

        var targetActorId = targetActivity.ActorId;
        if (!string.IsNullOrEmpty(targetActorId))
        {
            await _repository.QueueSharedInboxDeliveryAsync(likeId, likeJson, targetActorId);
        }

        _logger.LogInformation("User {Username} liked activity {ActivityId}", username, targetActivityId);
        TempData["InteractionSuccess"] = "Post liked!";
        return RedirectToAction("Index", "Timeline");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlike(string targetActivityId)
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return RedirectToAction("Index", "Timeline");
        }

        var existingLike = await _repository.GetLikeByActorAsync(username, targetActivityId);
        if (existingLike == null)
        {
            return RedirectToAction("Index", "Timeline");
        }

        var now = DateTime.UtcNow;
        var undoId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var undoActivity = new Activity
        {
            Id = undoId,
            Type = "Undo",
            Actor = actor.Id,
            Object = existingLike,
            Published = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        await _repository.SaveActivityAsync(undoActivity);

        var targetActivity = await _repository.GetActivityAsync(targetActivityId);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var undoJson = JsonSerializer.Serialize(undoActivity, jsonOptions);

        if (targetActivity != null)
        {
            var targetActorId = targetActivity.ActorId;
            if (!string.IsNullOrEmpty(targetActorId))
            {
                await _repository.QueueSharedInboxDeliveryAsync(undoId, undoJson, targetActorId);
            }
        }

        await _repository.DeleteActivityAsync(existingLike);
        _logger.LogInformation("User {Username} unliked activity {ActivityId}", username, targetActivityId);
        return RedirectToAction("Index", "Timeline");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(string targetActivityId, string content)
    {
        var username = User.Identity!.Name!;

        if (string.IsNullOrWhiteSpace(content) || content.Length > 500)
        {
            TempData["ReplyError"] = "Reply must be between 1 and 500 characters.";
            return RedirectToAction("Index", "Timeline", new { activityId = targetActivityId });
        }

        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            TempData["ReplyError"] = "Federation account not found.";
            return RedirectToAction("Index", "Timeline");
        }

        var now = DateTime.UtcNow;
        var noteId = $"https://localhost/users/{username}/notes/{Guid.NewGuid()}";
        var activityId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var note = new Note
        {
            Id = noteId,
            Type = "Note",
            Content = System.Net.WebUtility.HtmlEncode(content),
            AttributedTo = actor.Id,
            Published = now,
            InReplyTo = targetActivityId,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        var activity = new Activity
        {
            Id = activityId,
            Type = "Create",
            Actor = actor.Id,
            Object = note,
            Published = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        await _repository.SaveActivityAsync(activity);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var activityJson = JsonSerializer.Serialize(activity, jsonOptions);

        var targetActivity = await _repository.GetActivityAsync(targetActivityId);
        if (targetActivity != null)
        {
            var targetActorId = targetActivity.ActorId;
            if (!string.IsNullOrEmpty(targetActorId) && targetActorId != actor.Id)
            {
                await _repository.QueueSharedInboxDeliveryAsync(activityId, activityJson, targetActorId);
            }
        }

        var followers = await _repository.GetUniqueFollowerIdsAsync(username);
        foreach (var followerId in followers)
        {
            await _repository.QueueSharedInboxDeliveryAsync(activityId, activityJson, followerId);
        }

        _logger.LogInformation("User {Username} replied to activity {ActivityId}", username, targetActivityId);
        TempData["InteractionSuccess"] = "Reply posted!";
        return RedirectToAction("Index", "Timeline");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Boost(string targetActivityId)
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return RedirectToAction("Index", "Timeline");
        }

        var targetActivity = await _repository.GetActivityAsync(targetActivityId);
        if (targetActivity == null)
        {
            _logger.LogWarning("Cannot boost activity {ActivityId}: not found", targetActivityId);
            return RedirectToAction("Index", "Timeline");
        }

        if (await _repository.IsBoostedByActorAsync(username, targetActivityId))
        {
            TempData["InteractionError"] = "You have already boosted this post.";
            return RedirectToAction("Index", "Timeline");
        }

        var now = DateTime.UtcNow;
        var announceId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var announceActivity = new Activity
        {
            Id = announceId,
            Type = "Announce",
            Actor = actor.Id,
            Object = targetActivity.Id,
            Published = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        await _repository.SaveActivityAsync(announceActivity);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var announceJson = JsonSerializer.Serialize(announceActivity, jsonOptions);

        var targetActorId = targetActivity.ActorId;
        if (!string.IsNullOrEmpty(targetActorId) && targetActorId != actor.Id)
        {
            await _repository.QueueSharedInboxDeliveryAsync(announceId, announceJson, targetActorId);
        }

        _logger.LogInformation("User {Username} boosted activity {ActivityId}", username, targetActivityId);
        TempData["InteractionSuccess"] = "Post boosted!";
        return RedirectToAction("Index", "Timeline");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unboost(string targetActivityId)
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return RedirectToAction("Index", "Timeline");
        }

        var existingAnnounce = await _repository.GetBoostByActorAsync(username, targetActivityId);
        if (existingAnnounce == null)
        {
            return RedirectToAction("Index", "Timeline");
        }

        var now = DateTime.UtcNow;
        var undoId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var undoActivity = new Activity
        {
            Id = undoId,
            Type = "Undo",
            Actor = actor.Id,
            Object = existingAnnounce,
            Published = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        await _repository.SaveActivityAsync(undoActivity);

        _logger.LogInformation("User {Username} unboosted activity {ActivityId}", username, targetActivityId);
        await _repository.DeleteActivityAsync(existingAnnounce);
        return RedirectToAction("Index", "Timeline");
    }
}
