using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class ComposeController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<ComposeController> _logger;

    public ComposeController(IActivityPubRepository repository, ILogger<ComposeController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(ComposeModel model)
    {
        var username = User.Identity!.Name!;

        if (string.IsNullOrWhiteSpace(model.Content) || model.Content.Length > 500)
        {
            ModelState.AddModelError("Content", "Content must be between 1 and 500 characters.");
            return View("Index", model);
        }

        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            ModelState.AddModelError("", "Federation account not found.");
            return View("Index", model);
        }

        var now = DateTime.UtcNow;
        var noteId = $"https://localhost/users/{username}/notes/{Guid.NewGuid()}";
        var activityId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var note = new Note
        {
            Id = noteId,
            Type = "Note",
            Content = System.Net.WebUtility.HtmlEncode(model.Content),
            AttributedTo = actor.Id,
            Published = now,
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

        var followers = await _repository.GetUniqueFollowerIdsAsync(username);
        foreach (var followerId in followers)
        {
            await _repository.QueueSharedInboxDeliveryAsync(activityId, activityJson, followerId);
        }

        _logger.LogInformation("User {Username} created note {NoteId}", username, noteId);
        TempData["ComposeSuccess"] = true;
        return RedirectToAction("Index", "Timeline");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string activityId)
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return RedirectToAction("Index", "Timeline");
        }

        var activity = await _repository.GetActivityAsync(activityId);
        if (activity == null || activity.ActorId != actor.Id)
        {
            _logger.LogWarning("Cannot delete activity {ActivityId}: not found or not owned by {Username}", activityId, username);
            return RedirectToAction("Index", "Timeline");
        }

        var tombstoneId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";
        var deleteActivity = new Activity
        {
            Id = tombstoneId,
            Type = "Delete",
            Actor = actor.Id,
            Object = new ActivityPub.Core.Models.Object
            {
                Id = activity.ObjectId ?? activity.Id,
                Type = "Tombstone"
            },
            Published = DateTime.UtcNow,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        await _repository.SaveActivityAsync(deleteActivity);
        await _repository.DeleteActivityAsync(activityId);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var deleteJson = JsonSerializer.Serialize(deleteActivity, jsonOptions);
        var followers = await _repository.GetUniqueFollowerIdsAsync(username);
        foreach (var followerId in followers)
        {
            await _repository.QueueSharedInboxDeliveryAsync(tombstoneId, deleteJson, followerId);
        }

        _logger.LogInformation("User {Username} deleted activity {ActivityId}", username, activityId);
        return RedirectToAction("Index", "Timeline");
    }
}

public class ComposeModel
{
    public string? Content { get; set; }
}
