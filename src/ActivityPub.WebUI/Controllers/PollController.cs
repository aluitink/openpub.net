using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.WebUI.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class PollController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<PollController> _logger;
    private readonly INotificationService _notificationService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public PollController(IActivityPubRepository repository, ILogger<PollController> logger, INotificationService notificationService)
    {
        _repository = repository;
        _logger = logger;
        _notificationService = notificationService;
    }

    [HttpGet]
    public IActionResult New(string content)
    {
        ViewData["Content"] = content;
        return View(new PollComposeModel { Content = content });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PollComposeModel model)
    {
        var username = User.Identity!.Name!;

        if (string.IsNullOrWhiteSpace(model.Content) || model.Content.Length > 500)
        {
            ModelState.AddModelError("Content", "Question must be between 1 and 500 characters.");
            return View("New", model);
        }

        var options = model.Options?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
        if (options == null || options.Count < 2 || options.Count > 4)
        {
            ModelState.AddModelError("Options", "Poll must have 2-4 options.");
            return View("New", model);
        }

        foreach (var opt in options)
        {
            if (opt.Length > 50)
            {
                ModelState.AddModelError("Options", "Each option must be 50 characters or less.");
                return View("New", model);
            }
        }

        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            ModelState.AddModelError("", "Federation account not found.");
            return View("New", model);
        }

        var now = DateTime.UtcNow;
        var pollId = $"https://localhost/users/{username}/polls/{Guid.NewGuid()}";
        var noteId = $"https://localhost/users/{username}/notes/{Guid.NewGuid()}";
        var activityId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

        var poll = new Poll
        {
            Id = pollId,
            Type = "Question",
            Name = model.Content,
            Options = options,
            EndTime = now.AddMinutes(model.DurationMinutes),
            Expires = true,
            Closed = false,
            VotesCount = 0
        };

        var note = new Note
        {
            Id = noteId,
            Type = "Note",
            Content = System.Net.WebUtility.HtmlEncode(model.Content),
            AttributedTo = actor.Id,
            Published = now,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };

        if (note.AdditionalProperties == null)
            note.AdditionalProperties = new Dictionary<string, JsonElement>();

        var pollElement = JsonSerializer.SerializeToElement(new
        {
            id = pollId,
            type = "Question",
            name = model.Content,
            options = options,
            endTime = now.AddMinutes(model.DurationMinutes),
            expires = true,
            closed = false,
            votesCount = 0
        });

        note.AdditionalProperties["attachment"] = JsonSerializer.SerializeToElement(new[] { pollElement });

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
        var followers = await _repository.GetUniqueFollowerIdsAsync(username);
        foreach (var followerId in followers)
        {
            await _repository.QueueSharedInboxDeliveryAsync(activityId, activityJson, followerId);
        }

        await _notificationService.BroadcastNewActivityAsync(activityId, "Poll", username, model.Content);

        _logger.LogInformation("User {Username} created poll {PollId}", username, pollId);
        TempData["ComposeSuccess"] = true;
        return RedirectToAction("Index", "Timeline");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(string pollId, string activityId, int optionIndex)
    {
        var username = User.Identity!.Name!;
        _logger.LogInformation("User {Username} voted on poll {PollId} option {Index}", username, pollId, optionIndex);

        var activity = await _repository.GetActivityAsync(activityId);
        if (activity == null)
            return NotFound();

        TempData["InteractionSuccess"] = "Vote recorded!";
        return RedirectToAction("Index", "Timeline");
    }
}

public class PollComposeModel
{
    public string? Content { get; set; }
    public List<string>? Options { get; set; } = new() { "", "", "", "" };
    public int DurationMinutes { get; set; } = 1440;
}
