using System.Text.Json;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using APObject = ActivityPub.Core.Models.Object;

namespace ActivityPub.WebUI.Controllers.Api;

/// <summary>
/// Maps ActivityPub core models (Activity / Actor) onto the Mastodon-compatible
/// API DTOs. Reuses the same attachment/sensitive/CW extraction approach as the
/// WebUI TimelineController so the REST surface stays consistent with the UI.
/// </summary>
public static class ApiMapper
{
    // Deterministic, URL-safe numeric ID derived from an ActivityPub activity URL.
    // Mastodon-style API clients expect numeric status IDs; the full URL is carried
    // in the 'uri' field.
    public static string ToApiStatusId(string activityId)
    {
        ulong hash = 1469598103934665603UL;
        foreach (var c in activityId)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash.ToString();
    }

    public static ApiAccount? ToAccount(Actor? actor)
    {
        if (actor == null)
            return null;

        var username = actor.PreferredUsername ?? (actor.Id?.Split('/').Last()) ?? string.Empty;
        return new ApiAccount
        {
            Id = actor.Id ?? string.Empty,
            Username = username,
            Acct = username,
            DisplayName = actor.Name ?? username,
            Note = actor.Summary ?? string.Empty,
            Url = actor.Url,
            Avatar = actor.Icon?.Url,
            AvatarStatic = actor.Icon?.Url,
            Header = actor.Image?.Url,
            HeaderStatic = actor.Image?.Url,
            Locked = actor.ManuallyApprovesFollowers,
            Bot = (actor.Type?.Contains("Bot", StringComparison.OrdinalIgnoreCase)) == true,
            Discoverable = true,
            Group = (actor.Type?.Equals("Group", StringComparison.OrdinalIgnoreCase)) == true,
            CreatedAt = actor.Published ?? DateTime.MinValue
        };
    }

    /// <summary>
    /// Build a status DTO for a single activity. <paramref name="viewerUsername"/>
    /// is the authenticated user (for favourited/reblogged flags) or null for
    /// anonymous requests.
    /// </summary>
    public static async Task<ApiStatus?> ToStatusAsync(
        IActivityPubRepository repository,
        Activity activity,
        string? viewerUsername)
    {
        var note = ExtractNote(activity);
        if (note == null)
            return null;

        var author = await GetAuthorActorAsync(repository, activity);
        var account = ToAccount(author);
        var activityId = activity.Id ?? string.Empty;

        var likeCount = await repository.GetLikeCountAsync(activityId);
        var boostCount = await repository.GetBoostCountAsync(activityId);
        var replyCount = await repository.GetReplyCountAsync(activityId);
        var isLiked = viewerUsername != null && await repository.IsLikedByActorAsync(viewerUsername, activityId);
        var isBoosted = viewerUsername != null && await repository.IsBoostedByActorAsync(viewerUsername, activityId);

        var isAnnounce = (activity.Type ?? "Create").Equals("Announce", StringComparison.OrdinalIgnoreCase);

        return new ApiStatus
        {
            Id = ToApiStatusId(activityId),
            Uri = activityId,
            Url = note.Url ?? activityId,
            Account = account,
            MediaAttachments = ExtractMediaAttachments(note),
            SpoilerText = ExtractContentWarning(note) ?? string.Empty,
            Content = note.Content ?? string.Empty,
            CreatedAt = note.Published ?? DateTime.UtcNow,
            InReplyToId = note.InReplyTo,
            Sensitive = ExtractSensitive(note),
            Visibility = "public",
            RepliesCount = replyCount,
            Reblogged = isAnnounce,
            ReblogsCount = boostCount,
            FavouritesCount = likeCount,
            FavouritedByMe = isLiked,
            Favourited = isLiked,
            RebloggedByMe = isBoosted,
            Poll = ExtractPoll(note)
        };
    }

    // ---------- extraction helpers (mirrors TimelineController) ----------

    static APObject? ExtractNote(Activity activity)
    {
        if (activity.Object is APObject obj)
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
            return element.Deserialize<APObject>(opts);
        }

        return null;
    }

    static async Task<Actor?> GetAuthorActorAsync(IActivityPubRepository repository, Activity activity)
    {
        var actorId = activity.ActorId;
        if (actorId == null)
            return null;
        var username = actorId.Split('/').Last();
        return await repository.GetUserActorAsync(username);
    }

    static JsonElement? GetAttachmentElement(APObject obj)
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

    static List<ApiMediaAttachment> ExtractMediaAttachments(APObject obj)
    {
        var result = new List<ApiMediaAttachment>();
        var elem = GetAttachmentElement(obj);
        if (elem is not { } att)
            return result;

        foreach (var item in att.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (!"Image".Equals(type, StringComparison.OrdinalIgnoreCase))
                continue;

            var url = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
            var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            var mediaType = item.TryGetProperty("mediaType", out var mtProp) ? mtProp.GetString() : null;

            var kind = "image";
            if (mediaType != null)
            {
                if (mediaType.StartsWith("video", StringComparison.OrdinalIgnoreCase)) kind = "video";
                else if (mediaType.StartsWith("audio", StringComparison.OrdinalIgnoreCase)) kind = "audio";
                else if (mediaType.StartsWith("image/gif", StringComparison.OrdinalIgnoreCase)) kind = "gifv";
                else if (mediaType.Contains("pdf", StringComparison.OrdinalIgnoreCase)) kind = "document";
            }

            result.Add(new ApiMediaAttachment
            {
                Type = kind,
                Url = url,
                PreviewUrl = url,
                RemoteUrl = url,
                Description = name
            });
        }

        return result;
    }

    static bool ExtractSensitive(APObject obj)
    {
        if (obj.AdditionalProperties != null && obj.AdditionalProperties.TryGetValue("sensitive", out var sensitiveVal))
        {
            if (sensitiveVal.ValueKind == JsonValueKind.True) return true;
            if (sensitiveVal.ValueKind == JsonValueKind.String && bool.TryParse(sensitiveVal.GetString(), out var b) && b) return true;
        }
        if (obj.Content != null && obj.Content.TrimStart().StartsWith("CW:", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string? ExtractContentWarning(APObject obj)
    {
        if (obj.AdditionalProperties != null && obj.AdditionalProperties.TryGetValue("contentWarning", out var cwVal))
        {
            var s = cwVal.ValueKind == JsonValueKind.String ? cwVal.GetString() : cwVal.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        if (obj.Content != null && obj.Content.TrimStart().StartsWith("CW:", StringComparison.OrdinalIgnoreCase))
        {
            var cw = obj.Content.TrimStart().Substring(3).Trim();
            if (!string.IsNullOrWhiteSpace(cw)) return cw;
        }
        return null;
    }

    static ApiPoll? ExtractPoll(APObject obj)
    {
        var elem = GetAttachmentElement(obj);
        if (elem is not { } att || att.GetArrayLength() == 0)
            return null;

        var first = att[0];
        if (first.ValueKind != JsonValueKind.Object)
            return null;

        if (!first.TryGetProperty("type", out var typeVal) ||
            !"Question".Equals(typeVal.GetString(), StringComparison.OrdinalIgnoreCase))
            return null;

        var options = new List<ApiPollOption>();
        if (first.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var opt in optionsProp.EnumerateArray())
            {
                options.Add(new ApiPollOption
                {
                    Title = opt.ValueKind == JsonValueKind.String ? opt.GetString() ?? "" : ""
                });
            }
        }

        DateTime? endTime = null;
        if (first.TryGetProperty("endTime", out var endTimeProp))
        {
            if (endTimeProp.ValueKind == JsonValueKind.String && DateTime.TryParse(endTimeProp.GetString(), out var parsed))
                endTime = parsed;
            else if (endTimeProp.ValueKind == JsonValueKind.Number)
                endTime = endTimeProp.GetDateTime();
        }

        var now = DateTime.UtcNow;
        return new ApiPoll
        {
            Id = first.TryGetProperty("id", out var idProp) ? idProp.GetString() : null,
            ExpiresAt = endTime,
            Expired = endTime != null && endTime <= now,
            Multiple = false,
            Options = options
        };
    }
}
