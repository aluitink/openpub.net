using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class TimelineController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<TimelineController> _logger;

    public TimelineController(IActivityPubRepository repository, ILogger<TimelineController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    [ResponseCache(Duration = 3, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Index(int page = 1)
    {
        var username = User.Identity!.Name!;
        var pageSize = 20;
        var skip = (page - 1) * pageSize;

        var outboxIds = await _repository.GetActorOutboxActivitiesAsync(username, skip, pageSize);
        var inboxIds = await _repository.GetInboxActivitiesAsync(username, skip, pageSize);

        var allIds = new HashSet<string>(outboxIds);
        allIds.UnionWith(inboxIds);

        var activities = new List<TimelineActivityItem>();
        foreach (var id in allIds)
        {
            var activity = await _repository.GetActivityAsync(id);
            if (activity != null)
            {
                var note = ExtractNote(activity);
                if (note != null)
                {
                    var authorActor = await GetAuthorActor(activity);
                    var likeCount = await _repository.GetLikeCountAsync(activity.Id);
                    var boostCount = await _repository.GetBoostCountAsync(activity.Id);
                    var replyCount = await _repository.GetReplyCountAsync(activity.Id);
                    var isLiked = await _repository.IsLikedByActorAsync(username, activity.Id);
                    var isBoosted = await _repository.IsBoostedByActorAsync(username, activity.Id);

                    activities.Add(new TimelineActivityItem
                    {
                        ActivityId = activity.Id!,
                        AuthorName = authorActor?.PreferredUsername ?? activity.ActorId?.Split('/').Last() ?? "unknown",
                        AuthorDisplayName = authorActor?.Name ?? "",
                        Content = note.Content ?? "",
                        Published = note.Published ?? DateTime.UtcNow,
                        ActivityType = activity.Type ?? "Create",
                        InReplyTo = note.InReplyTo,
                        LikeCount = likeCount,
                        BoostCount = boostCount,
                        ReplyCount = replyCount,
                        IsLiked = isLiked,
                        IsBoosted = isBoosted,
                        ImageUrl = ExtractImageUrl(note),
                        PollQuestion = ExtractPollQuestion(note),
                        PollOptions = ExtractPollOptions(note),
                        PollEndTime = ExtractPollEndTime(note),
                        PollId = ExtractPollId(note)
                    });
                }
            }
        }

        activities.Sort((a, b) => b.Published.CompareTo(a.Published));
        ViewBag.ComposeSuccess = TempData["ComposeSuccess"];
        ViewBag.InteractionSuccess = TempData["InteractionSuccess"];
        ViewBag.InteractionError = TempData["InteractionError"];
        ViewBag.ReplyError = TempData["ReplyError"];
        return View(activities);
    }

    static ActivityPub.Core.Models.Object? ExtractNote(Activity activity)
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

            var opts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            return element.Deserialize<ActivityPub.Core.Models.Object>(opts);
        }

        return null;
    }

    static string? ExtractImageUrl(ActivityPub.Core.Models.Object obj)
    {
        if (obj.AdditionalProperties?.TryGetValue("attachment", out var attachment) == true && attachment is System.Text.Json.JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Array && elem.GetArrayLength() > 0)
            {
                var first = elem[0];
                if (first.TryGetProperty("url", out var urlProp))
                    return urlProp.GetString();
            }
        }
        return null;
    }

    static string? ExtractPollQuestion(ActivityPub.Core.Models.Object obj)
    {
        if (obj.AdditionalProperties?.TryGetValue("attachment", out var attachment) == true && attachment is System.Text.Json.JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Array && elem.GetArrayLength() > 0)
            {
                var first = elem[0];
                if (first.TryGetProperty("type", out var typeVal) && "Question".Equals(typeVal.GetString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (first.TryGetProperty("name", out var nameProp))
                        return nameProp.GetString();
                }
            }
        }
        return null;
    }

    static List<PollOptionItem>? ExtractPollOptions(ActivityPub.Core.Models.Object obj)
    {
        if (obj.AdditionalProperties?.TryGetValue("attachment", out var attachment) == true && attachment is System.Text.Json.JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Array && elem.GetArrayLength() > 0)
            {
                var first = elem[0];
                if (first.TryGetProperty("type", out var typeVal) && "Question".Equals(typeVal.GetString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (first.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Array)
                    {
                        var options = new List<PollOptionItem>();
                        foreach (var opt in optionsProp.EnumerateArray())
                        {
                            options.Add(new PollOptionItem { Text = opt.GetString() ?? "" });
                        }
                        return options;
                    }
                }
            }
        }
        return null;
    }

    static DateTime? ExtractPollEndTime(ActivityPub.Core.Models.Object obj)
    {
        if (obj.AdditionalProperties?.TryGetValue("attachment", out var attachment) == true && attachment is System.Text.Json.JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Array && elem.GetArrayLength() > 0)
            {
                var first = elem[0];
                if (first.TryGetProperty("endTime", out var endTimeProp))
                    return endTimeProp.GetDateTime();
            }
        }
        return null;
    }

    static string? ExtractPollId(ActivityPub.Core.Models.Object obj)
    {
        if (obj.AdditionalProperties?.TryGetValue("attachment", out var attachment) == true && attachment is System.Text.Json.JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Array && elem.GetArrayLength() > 0)
            {
                var first = elem[0];
                if (first.TryGetProperty("id", out var idProp))
                    return idProp.GetString();
            }
        }
        return null;
    }

    async Task<Actor?> GetAuthorActor(Activity activity)
    {
        var actorId = activity.ActorId;
        if (actorId == null)
            return null;

        var username = actorId.Split('/').Last();
        return await _repository.GetUserActorAsync(username);
    }
}

public class TimelineActivityItem
{
    public string ActivityId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Published { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? InReplyTo { get; set; }
    public int LikeCount { get; set; }
    public int BoostCount { get; set; }
    public int ReplyCount { get; set; }
    public bool IsLiked { get; set; }
    public bool IsBoosted { get; set; }
    public string? ImageUrl { get; set; }
    public string? PollQuestion { get; set; }
    public List<PollOptionItem>? PollOptions { get; set; }
    public DateTime? PollEndTime { get; set; }
    public string? PollId { get; set; }
}

public class PollOptionItem
{
    public string Text { get; set; } = string.Empty;
    public int? Votes { get; set; } = 0;
    public int Percent => 0;
}
