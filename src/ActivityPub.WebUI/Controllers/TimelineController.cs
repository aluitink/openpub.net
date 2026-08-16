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
                        AuthorAvatarUrl = authorActor?.Icon?.ToString(),
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
                        Sensitive = ExtractSensitive(note),
                        ContentWarning = ExtractContentWarning(note),
                        DocumentAttachments = ExtractDocumentAttachments(note),
                        PollQuestion = ExtractPollQuestion(note),
                        PollOptions = ExtractPollOptions(note),
                        PollEndTime = ExtractPollEndTime(note),
                        PollId = ExtractPollId(note)
                    });
                }
            }
        }

        activities.Sort((a, b) => b.Published.CompareTo(a.Published));
        ViewBag.Page = page;
        ViewBag.HasMore = activities.Count == pageSize;
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

    static JsonElement? GetAttachmentElement(ActivityPub.Core.Models.Object obj)
    {
        var collection = obj.Attachment;
        if (collection is { Count: > 0 })
        {
            var items = collection
                .Select(item => item switch
                {
                    JsonElement je => je,
                    string s => JsonDocument.Parse(s).RootElement,
                    _ => default
                })
                .Where(e => e.ValueKind == JsonValueKind.Object)
                .ToList();
            if (items.Count > 0)
                return JsonSerializer.SerializeToElement(items);
        }

        if (obj.AdditionalProperties?.TryGetValue("attachment", out var attachment) == true &&
            attachment.ValueKind == JsonValueKind.Array)
        {
            return attachment;
        }

        return null;
    }

    static string? ExtractImageUrl(ActivityPub.Core.Models.Object obj)
    {
        var elem = GetAttachmentElement(obj);
        if (elem is { } att)
        {
            foreach (var item in att.EnumerateArray())
            {
                var type = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("type", out var t) ? t.GetString() : null;
                if ("Image".Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    if (item.TryGetProperty("url", out var urlProp))
                        return urlProp.GetString();
                }
            }
            if (att.GetArrayLength() > 0)
            {
                var first = att[0];
                if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("url", out var urlProp))
                    return urlProp.GetString();
            }
        }
        return null;
    }

    static bool ExtractSensitive(ActivityPub.Core.Models.Object obj)
    {
        if (obj.AdditionalProperties != null)
        {
            if (obj.AdditionalProperties.TryGetValue("sensitive", out var sensitiveVal))
            {
                if (sensitiveVal.ValueKind == JsonValueKind.True) return true;
                if (sensitiveVal.ValueKind == JsonValueKind.String && bool.TryParse(sensitiveVal.GetString(), out var b) && b) return true;
            }
        }
        if (obj.Content != null && obj.Content.TrimStart().StartsWith("CW:", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string? ExtractContentWarning(ActivityPub.Core.Models.Object obj)
    {
        if (obj.AdditionalProperties != null)
        {
            if (obj.AdditionalProperties.TryGetValue("contentWarning", out var cwVal))
            {
                var s = cwVal.ValueKind == JsonValueKind.String ? cwVal.GetString() : cwVal.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }
        if (obj.Content != null && obj.Content.TrimStart().StartsWith("CW:", StringComparison.OrdinalIgnoreCase))
        {
            var cw = obj.Content.TrimStart().Substring(3).Trim();
            if (!string.IsNullOrWhiteSpace(cw)) return cw;
        }
        return null;
    }

    static List<DocumentAttachmentItem>? ExtractDocumentAttachments(ActivityPub.Core.Models.Object obj)
    {
        var elem = GetAttachmentElement(obj);
        if (elem is not { } att)
            return null;

        var docs = new List<DocumentAttachmentItem>();
        foreach (var item in att.EnumerateArray())
        {
            var type = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (!"Document".Equals(type, StringComparison.OrdinalIgnoreCase))
                continue;

            docs.Add(new DocumentAttachmentItem
            {
                Url = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "",
                Name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : ""
            });
        }
        return docs.Count > 0 ? docs : null;
    }

    static JsonElement? GetPollElement(ActivityPub.Core.Models.Object obj)
    {
        var elem = GetAttachmentElement(obj);
        if (elem is { } att && att.GetArrayLength() > 0)
        {
            var first = att[0];
            if (first.ValueKind == JsonValueKind.Object)
                return first;
        }
        return null;
    }

    static string? ExtractPollQuestion(ActivityPub.Core.Models.Object obj)
    {
        var poll = GetPollElement(obj);
        if (poll is { } p)
        {
            if (p.TryGetProperty("type", out var typeVal) && "Question".Equals(typeVal.GetString(), StringComparison.OrdinalIgnoreCase))
            {
                if (p.TryGetProperty("name", out var nameProp))
                    return nameProp.GetString();
            }
        }
        return null;
    }

    static List<PollOptionItem>? ExtractPollOptions(ActivityPub.Core.Models.Object obj)
    {
        var poll = GetPollElement(obj);
        if (poll is { } p)
        {
            if (p.TryGetProperty("type", out var typeVal) && "Question".Equals(typeVal.GetString(), StringComparison.OrdinalIgnoreCase))
            {
                if (p.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Array)
                {
                    var options = new List<PollOptionItem>();
                    foreach (var opt in optionsProp.EnumerateArray())
                    {
                        options.Add(new PollOptionItem { Text = opt.ValueKind == JsonValueKind.String ? opt.GetString() ?? "" : "" });
                    }
                    return options;
                }
            }
        }
        return null;
    }

    static DateTime? ExtractPollEndTime(ActivityPub.Core.Models.Object obj)
    {
        var poll = GetPollElement(obj);
        if (poll is { } p)
        {
            if (p.TryGetProperty("endTime", out var endTimeProp))
                return endTimeProp.GetDateTime();
        }
        return null;
    }

    static string? ExtractPollId(ActivityPub.Core.Models.Object obj)
    {
        var poll = GetPollElement(obj);
        if (poll is { } p)
        {
            if (p.TryGetProperty("id", out var idProp))
                return idProp.GetString();
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
    public string? AuthorAvatarUrl { get; set; }
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
    public bool Sensitive { get; set; }
    public string? ContentWarning { get; set; }
    public List<DocumentAttachmentItem>? DocumentAttachments { get; set; }
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

public class DocumentAttachmentItem
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
