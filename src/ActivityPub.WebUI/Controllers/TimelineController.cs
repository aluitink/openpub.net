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
        // The timeline ends only when both feeds run short: each query is
        // independent, so a full page from either one still has a next page.
        var pageFull = outboxIds.Count == pageSize && inboxIds.Count == pageSize;

        var blurSensitive = await GetBlurPrefAsync(username);

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
                    var activityId = activity.Id ?? string.Empty;
                    var likeCount = await _repository.GetLikeCountAsync(activityId);
                    var boostCount = await _repository.GetBoostCountAsync(activityId);
                    var replyCount = await _repository.GetReplyCountAsync(activityId);
                    var isLiked = await _repository.IsLikedByActorAsync(username, activityId);
                    var isBoosted = await _repository.IsBoostedByActorAsync(username, activityId);

                    activities.Add(new TimelineActivityItem
                    {
                        ActivityId = activityId,
                        AuthorName = authorActor?.PreferredUsername ?? activity.ActorId?.Split('/').Last() ?? "unknown",
                        AuthorDisplayName = authorActor?.Name ?? "",
                        AuthorAvatarUrl = authorActor?.Icon?.Url,
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
                        ImageAttachments = ExtractImageAttachments(note),
                        Sensitive = ExtractSensitive(note),
                        ContentWarning = ExtractContentWarning(note),
                        BlurSensitive = blurSensitive,
                        DocumentAttachments = ExtractDocumentAttachments(note),
                        MediaAttachments = ExtractMediaAttachments(note),
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
        ViewBag.HasMore = pageFull;
        ViewBag.ComposeSuccess = TempData["ComposeSuccess"];
        ViewBag.InteractionSuccess = TempData["InteractionSuccess"];
        ViewBag.InteractionError = TempData["InteractionError"];
        ViewBag.ReplyError = TempData["ReplyError"];
        return View(activities);
    }

    /// <summary>
    /// Renders a single note card as an HTML fragment for live (SignalR/SSE)
    /// timeline inserts. Returns 404 if the activity is unknown or not a note.
    /// </summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Card(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        // The id is a full ActivityPub URL. MVC decodes the route value, but
        // clients (JS encodeURIComponent / HttpClient) may hand us a still-
        // percent-encoded path segment; normalize either way so the lookup
        // always matches the stored ActivityId.
        var lookupId = id.Contains("%") ? Uri.UnescapeDataString(id) : id;
        var username = User.Identity!.Name!;
        var activity = await _repository.GetActivityAsync(lookupId);
        if (activity == null)
            return NotFound();

        var note = ExtractNote(activity);
        if (note == null)
            return NotFound();

        var authorActor = await GetAuthorActor(activity);
        var activityId = activity.Id ?? string.Empty;
        var likeCount = await _repository.GetLikeCountAsync(activityId);
        var boostCount = await _repository.GetBoostCountAsync(activityId);
        var replyCount = await _repository.GetReplyCountAsync(activityId);
        var isLiked = await _repository.IsLikedByActorAsync(username, activityId);
        var isBoosted = await _repository.IsBoostedByActorAsync(username, activityId);

        var blurSensitive = await GetBlurPrefAsync(username);
        var item = new TimelineActivityItem
        {
            ActivityId = activityId,
            AuthorName = authorActor?.PreferredUsername ?? activity.ActorId?.Split('/').Last() ?? "unknown",
            AuthorDisplayName = authorActor?.Name ?? "",
            AuthorAvatarUrl = authorActor?.Icon?.Url,
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
            ImageAttachments = ExtractImageAttachments(note),
            Sensitive = ExtractSensitive(note),
            ContentWarning = ExtractContentWarning(note),
            BlurSensitive = blurSensitive,
            DocumentAttachments = ExtractDocumentAttachments(note),
            MediaAttachments = ExtractMediaAttachments(note),
            PollQuestion = ExtractPollQuestion(note),
            PollOptions = ExtractPollOptions(note),
            PollEndTime = ExtractPollEndTime(note),
            PollId = ExtractPollId(note)
        };

        return PartialView("_NoteCard", item);
    }

    static ActivityPub.Core.Models.Object? ExtractNote(Activity activity)
    {
        // The Object property may be a typed Object (built in-process) or a
        // JsonElement (rehydrated from stored JsonData). Normalize either to a
        // typed Object so notes — including the target of an Announce/boost —
        // render with the correct interaction counts and author.
        ActivityPub.Core.Models.Object? note = activity.Object switch
        {
            ActivityPub.Core.Models.Object o => o,
            JsonElement je when je.ValueKind == JsonValueKind.Object
                => SafeDeserializeObject(je),
            _ => null
        };

        if (note == null)
            return null;
        return note.Type == "Tombstone" ? null : note;
    }

    /// <summary>
    /// Deserializes a stored note, tolerating malformed / foreign activity
    /// (e.g. a remote instance sending a note that is missing a required field).
    /// A single bad activity must not 500 the whole timeline, so any parse
    /// failure yields null and the activity is simply skipped.
    /// </summary>
    static ActivityPub.Core.Models.Object? SafeDeserializeObject(JsonElement element)
    {
        try
        {
            return DeserializeObject(element);
        }
        catch (Exception)
        {
            return null;
        }
    }

    static ActivityPub.Core.Models.Object? DeserializeObject(JsonElement element)
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

    /// <summary>
    /// Legacy single-image URL. Only ever returns a real Image attachment — the
    /// old "first attachment" fallback is gone because non-image media (video /
    /// audio / document) is now surfaced separately and must not be mis-rendered
    /// as an &lt;img&gt;.
    /// </summary>
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
        }
        return null;
    }

    static List<NoteImageItem>? ExtractImageAttachments(ActivityPub.Core.Models.Object obj)
    {
        var elem = GetAttachmentElement(obj);
        if (elem is not { } att)
            return null;

        var images = new List<NoteImageItem>();
        foreach (var item in att.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (!"Image".Equals(type, StringComparison.OrdinalIgnoreCase)) continue;

            if (!item.TryGetProperty("url", out var urlProp) || string.IsNullOrWhiteSpace(urlProp.GetString()))
                continue;

            // Alt text: Mastodon-style "alt" property, falling back to "name"
            // (the original filename) so screen readers never get a blank.
            var alt = "";
            if (item.TryGetProperty("alt", out var altProp) && !string.IsNullOrWhiteSpace(altProp.GetString()))
                alt = altProp.GetString()!;
            else if (item.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()))
                alt = nameProp.GetString()!;

            images.Add(new NoteImageItem
            {
                Url = urlProp.GetString()!,
                Alt = alt,
                Media = item.TryGetProperty("mediaType", out var mt) ? mt.GetString() : null
            });
        }
        return images.Count > 0 ? images : null;
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

    /// <summary>
    /// Extracts every non-image attachment (Video / Audio / Document) so the UI can
    /// render native players and download cards. Images are handled separately by
    /// <see cref="ExtractImageAttachments"/>. The wire shape is not fully under our
    /// control (remote instances), so each field is read defensively.
    /// </summary>
    static List<MediaAttachmentItem>? ExtractMediaAttachments(ActivityPub.Core.Models.Object obj)
    {
        var elem = GetAttachmentElement(obj);
        if (elem is not { } att)
            return null;

        var media = new List<MediaAttachmentItem>();
        foreach (var item in att.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(type))
                continue;

            var kind = type.ToLowerInvariant();
            if ("image".Equals(kind, StringComparison.Ordinal) || "gif".Equals(kind, StringComparison.Ordinal))
                continue; // handled by the image extractor / lightbox

            var mediaType = item.TryGetProperty("mediaType", out var mt) ? mt.GetString() : null;

            // Refine the kind from the mediaType prefix when the ActivityPub type is
            // generic (e.g. "Document" carrying a video/* stream).
            if (mediaType != null)
            {
                if (mediaType.StartsWith("video", StringComparison.OrdinalIgnoreCase)) kind = "video";
                else if (mediaType.StartsWith("audio", StringComparison.OrdinalIgnoreCase)) kind = "audio";
                else if (kind == "document") kind = "document";
            }

            var url = GetAttachmentUrl(item);
            if (string.IsNullOrWhiteSpace(url))
                continue;

            var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
            var preview = GetAttachmentUrl(item, "preview");

            media.Add(new MediaAttachmentItem
            {
                Kind = kind,
                Url = url,
                Name = name,
                Media = mediaType,
                Preview = preview
            });
        }
        return media.Count > 0 ? media : null;
    }

    /// <summary>
    /// Reads a string attachment property (e.g. "url") that may be a plain string
    /// or an object with a "href" (strict ActivityPub Link form).
    /// </summary>
    static string? GetAttachmentUrl(JsonElement item, string prop = "url")
    {
        if (!item.TryGetProperty(prop, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Object => p.TryGetProperty("href", out var h) ? h.GetString() : null,
            _ => null
        };
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

    /// <summary>
    /// Resolves the viewer's "blur sensitive media" preference, defaulting to
    /// true (blur) when it has never been set.
    /// </summary>
    async Task<bool> GetBlurPrefAsync(string username)
    {
        var pref = await _repository.GetBlurSensitiveMediaAsync(username);
        return pref ?? true;
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
    public List<NoteImageItem>? ImageAttachments { get; set; }
    public bool Sensitive { get; set; }
    public string? ContentWarning { get; set; }
    /// <summary>
    /// The viewer's global "blur sensitive media" preference. When false the
    /// client should NOT auto-hide sensitive/CW content (the banner still shows
    /// so the user can blur it manually); when true (the default) sensitive/CW
    /// content starts blurred.
    /// </summary>
    public bool BlurSensitive { get; set; } = true;
    public List<DocumentAttachmentItem>? DocumentAttachments { get; set; }
    public List<MediaAttachmentItem>? MediaAttachments { get; set; }
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

/// <summary>
/// A non-image attachment (video, audio, or document) on a note, rendered with
/// a native &lt;video&gt;/&lt;audio&gt; player or a download card.
/// </summary>
public class MediaAttachmentItem
{
    /// <summary>"video", "audio", or "document" (also "gifv" for animated gifs).</summary>
    public string Kind { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Media { get; set; }
    /// <summary>Optional poster/thumbnail image (Mastodon "preview" property).</summary>
    public string? Preview { get; set; }
}

public class NoteImageItem
{
    public string Url { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
    public string? Media { get; set; }
}
