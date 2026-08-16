using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.WebUI.Services;
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
    private readonly INotificationService _notificationService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ComposeController(
        IActivityPubRepository repository,
        ILogger<ComposeController> logger,
        INotificationService notificationService)
    {
        _repository = repository;
        _logger = logger;
        _notificationService = notificationService;
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

        var activityJson = JsonSerializer.Serialize(activity, JsonOptions);
        await DistributeToFollowerInboxes(username, activityId, activityJson);

        await _notificationService.BroadcastNewActivityAsync(activityId, "Note", username, model.Content);

        _logger.LogInformation("User {Username} created note {NoteId}", username, noteId);
        TempData["ComposeSuccess"] = true;
        return RedirectToAction("Index", "Timeline");
    }

    [HttpGet]
    public IActionResult NewArticle() => View("Article", new ArticleComposeModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostArticle(ArticleComposeModel model)
    {
        var username = User.Identity!.Name!;

        if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Length > 200)
        {
            ModelState.AddModelError("Name", "Title must be between 1 and 200 characters.");
            return View("Article", model);
        }

        if (string.IsNullOrWhiteSpace(model.Content) || model.Content.Length > 50000)
        {
            ModelState.AddModelError("Content", "Content must be between 1 and 50,000 characters.");
            return View("Article", model);
        }

        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            ModelState.AddModelError("", "Federation account not found.");
            return View("Article", model);
        }

        var now = DateTime.UtcNow;
        var articleId = $"https://localhost/users/{username}/articles/{Guid.NewGuid()}";
        var activityId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var article = new Article
        {
            Id = articleId,
            Type = "Article",
            Name = model.Name,
            Summary = string.IsNullOrEmpty(model.Summary) ? model.Content[..Math.Min(200, model.Content.Length)] : model.Summary,
            Content = System.Net.WebUtility.HtmlEncode(model.Content),
            MediaType = "text/html",
            Url = $"https://localhost/users/{username}/articles/{articleId}",
            AttributedTo = actor.Id,
            Published = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        var activity = new Activity
        {
            Id = activityId,
            Type = "Create",
            Actor = actor.Id,
            Object = article,
            Published = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        await _repository.SaveActivityAsync(activity);

        var activityJson = JsonSerializer.Serialize(activity, JsonOptions);
        await DistributeToFollowerInboxes(username, activityId, activityJson);

        await _notificationService.BroadcastNewActivityAsync(activityId, "Article", username, model.Name);

        _logger.LogInformation("User {Username} created article {ArticleId}", username, articleId);
        TempData["ComposeSuccess"] = true;
        return RedirectToAction("Index", "Timeline");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditNote(EditNoteModel model)
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return RedirectToAction("Index", "Timeline");
        }

        if (string.IsNullOrWhiteSpace(model.Content) || model.Content.Length > 500)
        {
            TempData["EditError"] = "Content must be between 1 and 500 characters.";
            return RedirectToAction("Index", "Timeline");
        }

        var originalActivity = await _repository.GetActivityAsync(model.ActivityId);
        if (originalActivity == null || originalActivity.ActorId != actor.Id)
        {
            _logger.LogWarning("Cannot edit activity {ActivityId}: not found or not owned by {Username}", model.ActivityId, username);
            return RedirectToAction("Index", "Timeline");
        }

        var now = DateTime.UtcNow;
        var updatedNote = new Note
        {
            Id = originalActivity.ObjectId ?? originalActivity.Id,
            Type = "Note",
            Content = System.Net.WebUtility.HtmlEncode(model.Content),
            AttributedTo = actor.Id,
            Published = originalActivity.Published,
            Updated = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        var updateActivity = new Activity
        {
            Id = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}",
            Type = "Update",
            Actor = actor.Id,
            Object = updatedNote,
            Published = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        await _repository.SaveActivityAsync(updateActivity);

        var updateJson = JsonSerializer.Serialize(updateActivity, JsonOptions);
        await DistributeToFollowerInboxes(username, updateActivity.Id!, updateJson);

        _logger.LogInformation("User {Username} updated note via activity {ActivityId}", username, model.ActivityId);
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

        var deleteJson = JsonSerializer.Serialize(deleteActivity, JsonOptions);
        await DistributeToFollowerInboxes(username, tombstoneId, deleteJson);

        _logger.LogInformation("User {Username} deleted activity {ActivityId}", username, activityId);
        return RedirectToAction("Index", "Timeline");
    }

    async Task DistributeToFollowerInboxes(string username, string activityId, string activityJson)
    {
        var followers = await _repository.GetUniqueFollowerIdsAsync(username);
        foreach (var followerId in followers)
        {
            await _repository.QueueSharedInboxDeliveryAsync(activityId, activityJson, followerId);
        }
    }
}

public class ComposeModel
{
    public string? Content { get; set; }
}

public class ArticleComposeModel
{
    public string? Name { get; set; }
    public string? Summary { get; set; }
    public string? Content { get; set; }
}

public class EditNoteModel
{
    public string? ActivityId { get; set; }
    public string? Content { get; set; }
}
