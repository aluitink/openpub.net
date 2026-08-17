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
public class ComposeController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<ComposeController> _logger;
    private readonly INotificationService _notificationService;
    private readonly IWebhookDeliveryService _webhookService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ComposeController(
        IActivityPubRepository repository,
        ILogger<ComposeController> logger,
        INotificationService notificationService,
        IWebhookDeliveryService webhookService)
    {
        _repository = repository;
        _logger = logger;
        _notificationService = notificationService;
        _webhookService = webhookService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? replyTo)
    {
        var model = new ComposeIndexViewModel { Compose = new ComposeModel() };

        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            var target = await _repository.GetActivityAsync(replyTo);
            if (target != null)
            {
                var note = ExtractReplyNote(target);
                var authorActor = target.ActorId is { } actorId
                    ? await _repository.GetUserActorAsync(actorId.Split('/').Last())
                    : null;

                model.ReplyTarget = new ReplyTarget
                {
                    ActivityId = target.Id!,
                    AuthorName = authorActor?.PreferredUsername ?? target.ActorId?.Split('/').Last() ?? "unknown",
                    AuthorDisplayName = authorActor?.Name ?? "",
                    Snippet = note?.Content ?? ""
                };
                model.Compose.InReplyTo = target.Id!;
            }
        }

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(ComposeModel model)
    {
        var username = User.Identity!.Name!;

        if (string.IsNullOrWhiteSpace(model.Content) || model.Content.Length > 500)
        {
            ModelState.AddModelError("Content", "Content must be between 1 and 500 characters.");
            return View("Index", new ComposeIndexViewModel { Compose = model });
        }

        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            ModelState.AddModelError("", "Federation account not found.");
            return View("Index", new ComposeIndexViewModel { Compose = model });
        }

        var now = DateTime.UtcNow;
        var noteId = $"https://localhost/users/{username}/notes/{Guid.NewGuid()}";
        var activityId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        List<Dictionary<string, object>>? attachments = null;
        if (model.Image != null && model.Image.Length > 0 && model.Image.Length <= 10 * 1024 * 1024)
        {
            var extension = Path.GetExtension(model.Image.FileName);
            var filename = $"{Guid.NewGuid():N}{extension}";
            var uploadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads");
            Directory.CreateDirectory(uploadPath);
            var filepath = Path.Combine(uploadPath, filename);
            using (var stream = new FileStream(filepath, FileMode.Create))
            {
                await model.Image.CopyToAsync(stream);
            }
            var url = $"/uploads/{filename}";
            attachments = new List<Dictionary<string, object>>
            {
                new()
                {
                    { "type", "Image" },
                    { "mediaType", model.Image.ContentType },
                    { "url", url },
                    { "name", model.Image.FileName }
                }
            };
            _logger.LogInformation("Uploaded image {Filename} for user {Username}", filename, username);
        }

        if (model.Document != null && model.Document.Length > 0 && model.Document.Length <= 20 * 1024 * 1024)
        {
            if (attachments == null)
                attachments = new List<Dictionary<string, object>>();

            var extension = Path.GetExtension(model.Document.FileName);
            var filename = $"{Guid.NewGuid():N}{extension}";
            var uploadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads");
            Directory.CreateDirectory(uploadPath);
            var filepath = Path.Combine(uploadPath, filename);
            using (var stream = new FileStream(filepath, FileMode.Create))
            {
                await model.Document.CopyToAsync(stream);
            }
            var url = $"/uploads/{filename}";
            attachments.Add(new Dictionary<string, object>
            {
                { "type", "Document" },
                { "mediaType", string.IsNullOrEmpty(model.Document.ContentType) ? "application/octet-stream" : model.Document.ContentType },
                { "url", url },
                { "name", model.Document.FileName }
            });
            _logger.LogInformation("Uploaded document {Filename} for user {Username}", filename, username);
        }

        var note = new Note
        {
            Id = noteId,
            Type = "Note",
            Content = System.Net.WebUtility.HtmlEncode(model.Content),
            AttributedTo = actor.Id,
            Published = now,
            InReplyTo = string.IsNullOrWhiteSpace(model.InReplyTo) ? null : model.InReplyTo,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        if (attachments != null)
        {
            note.Attachment = attachments.Select(a => (object)JsonSerializer.SerializeToElement(a)).ToList();
        }

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

        if (!string.IsNullOrWhiteSpace(model.InReplyTo))
        {
            var targetActivity = await _repository.GetActivityAsync(model.InReplyTo);
            var targetActorId = targetActivity?.ActorId;
            if (!string.IsNullOrEmpty(targetActorId) && targetActorId != actor.Id)
            {
                await _repository.QueueSharedInboxDeliveryAsync(activityId, activityJson, targetActorId);
            }
        }

        await _notificationService.BroadcastNewActivityAsync(activityId, "Note", username, model.Content);

        // Notify external webhook subscribers (durable, async — a background
        // service delivers to each registered endpoint). Failures here must
        // never block the post, so swallow + log.
        try
        {
            await _webhookService.DeliverActivityToWebhooksAsync(activity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook enqueue failed for note {NoteId}", noteId);
        }

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

        var originalActivity = await _repository.GetActivityAsync(model.ActivityId ?? string.Empty);
        if (originalActivity == null || originalActivity.ActorId != actor.Id)
        {
            _logger.LogWarning("Cannot edit activity {ActivityId}: not found or not owned by {Username}", model.ActivityId, username);
            return RedirectToAction("Index", "Timeline");
        }

        var now = DateTime.UtcNow;
        var updatedNote = new Note
        {
            Id = originalActivity.ObjectId ?? originalActivity.Id ?? string.Empty,
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
                Id = activity.ObjectId ?? activity.Id ?? string.Empty,
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

    static ActivityPub.Core.Models.Object? ExtractReplyNote(Activity activity)
    {
        if (activity.Object is ActivityPub.Core.Models.Object obj)
        {
            return obj.Type == "Tombstone" ? null : obj;
        }

        if (activity.Object is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            var typeProp = element.TryGetProperty("type", out var typeVal) ? typeVal.GetString() : null;
            if (typeProp == "Tombstone")
                return null;

            return element.Deserialize<ActivityPub.Core.Models.Object>(JsonOptions);
        }

        return null;
    }
}

public class ComposeModel
{
    public string? Content { get; set; }
    public string? InReplyTo { get; set; }
    public IFormFile? Image { get; set; }
    public IFormFile? Document { get; set; }
}

public class ComposeIndexViewModel
{
    public ComposeModel Compose { get; set; } = new();
    public ReplyTarget? ReplyTarget { get; set; }
}

public class ReplyTarget
{
    public string ActivityId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
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
