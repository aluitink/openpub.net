using System.Text.Json.Serialization;

namespace ActivityPub.WebUI.Controllers.Api;

/// <summary>
/// Mastodon-compatible API DTOs for the local /api/v1 REST surface.
/// Property names are pinned with [JsonPropertyName] so the output is
/// stable regardless of the app-wide System.Text.Json naming policy.
/// </summary>

public class ApiStatus
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("uri")]
    public string? Uri { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("account")]
    public ApiAccount? Account { get; init; }

    [JsonPropertyName("media_attachments")]
    public List<ApiMediaAttachment> MediaAttachments { get; init; } = new();

    [JsonPropertyName("spoiler_text")]
    public string SpoilerText { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("in_reply_to_id")]
    public string? InReplyToId { get; init; }

    [JsonPropertyName("sensitive")]
    public bool Sensitive { get; init; }

    [JsonPropertyName("visibility")]
    public string Visibility { get; init; } = "public";

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("replies_count")]
    public int RepliesCount { get; init; }

    [JsonPropertyName("reblogged")]
    public bool Reblogged { get; init; }

    [JsonPropertyName("favourited")]
    public bool Favourited { get; init; }

    [JsonPropertyName("reblogs_count")]
    public int ReblogsCount { get; init; }

    [JsonPropertyName("favourites_count")]
    public int FavouritesCount { get; init; }

    [JsonPropertyName("favourited_by_me")]
    public bool FavouritedByMe { get; init; }

    [JsonPropertyName("reblogged_by_me")]
    public bool RebloggedByMe { get; init; }

    [JsonPropertyName("muted")]
    public bool Muted { get; init; }

    [JsonPropertyName("bookmarked")]
    public bool Bookmarked { get; init; }

    [JsonPropertyName("poll")]
    public ApiPoll? Poll { get; init; }

    [JsonPropertyName("reblog")]
    public ApiStatus? Reblog { get; init; }
}

public class ApiAccount
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("acct")]
    public string Acct { get; init; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("note")]
    public string Note { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; init; }

    [JsonPropertyName("avatar_static")]
    public string? AvatarStatic { get; init; }

    [JsonPropertyName("header")]
    public string? Header { get; init; }

    [JsonPropertyName("header_static")]
    public string? HeaderStatic { get; init; }

    [JsonPropertyName("locked")]
    public bool Locked { get; init; }

    [JsonPropertyName("bot")]
    public bool Bot { get; init; }

    [JsonPropertyName("discoverable")]
    public bool Discoverable { get; init; }

    [JsonPropertyName("group")]
    public bool Group { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("followers_count")]
    public int FollowersCount { get; set; }

    [JsonPropertyName("following_count")]
    public int FollowingCount { get; set; }

    [JsonPropertyName("statuses_count")]
    public int StatusesCount { get; set; }
}

public class ApiMediaAttachment
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "image";

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("preview_url")]
    public string? PreviewUrl { get; init; }

    [JsonPropertyName("remote_url")]
    public string? RemoteUrl { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public class ApiPoll
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }

    [JsonPropertyName("expired")]
    public bool Expired { get; init; }

    [JsonPropertyName("multiple")]
    public bool Multiple { get; init; }

    [JsonPropertyName("votes_count")]
    public int VotesCount { get; init; }

    [JsonPropertyName("voted")]
    public bool Voted { get; init; }

    [JsonPropertyName("own_votes")]
    public List<int> OwnVotes { get; init; } = new();

    [JsonPropertyName("options")]
    public List<ApiPollOption> Options { get; init; } = new();
}

public class ApiPollOption
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("votes_count")]
    public int VotesCount { get; init; }
}
