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

/// <summary>
/// Request body for POST /api/v1/apps (Mastodon application registration).
/// </summary>
public class ApiAppRegistrationRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("redirect_uris")]
    public string? RedirectUris { get; init; }

    [JsonPropertyName("scopes")]
    public string? Scopes { get; init; }

    [JsonPropertyName("website")]
    public string? Website { get; init; }
}

/// <summary>
/// Response body for POST /api/v1/apps and GET /api/v1/apps (the latter omits
/// the client secret, which is only shown once at creation).
/// </summary>
public class ApiApp
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("website")]
    public string? Website { get; init; }

    [JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; init; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    // Omitted on GET /api/v1/apps (the secret is only shown once, at creation).
    [JsonPropertyName("client_secret")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("vapid_key")]
    public string VapidKey { get; init; } = string.Empty;
}

/// <summary>
/// Request body for POST /api/v1/apps/webhooks (register a webhook endpoint
/// for the current user's account).
/// </summary>
public class ApiWebhookCreateRequest
{
    [JsonPropertyName("endpoint_url")]
    public string? EndpointUrl { get; init; }

    [JsonPropertyName("http_method")]
    public string? HttpMethod { get; init; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; init; }

    [JsonPropertyName("secret_key")]
    public string? SecretKey { get; init; }

    [JsonPropertyName("max_retries")]
    public int? MaxRetries { get; init; }

    [JsonPropertyName("retry_delay_seconds")]
    public int? RetryDelaySeconds { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }
}

/// <summary>
/// Response for a single webhook subscription. The secret key is echoed only
/// when it was provided at creation time (returned by POST); it is omitted on
/// GET listings.
/// </summary>
public class ApiWebhook
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("endpoint_url")]
    public string EndpointUrl { get; init; } = string.Empty;

    [JsonPropertyName("http_method")]
    public string HttpMethod { get; init; } = "POST";

    [JsonPropertyName("event_type")]
    public string EventType { get; init; } = "All";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("max_retries")]
    public int MaxRetries { get; init; }

    [JsonPropertyName("retry_delay_seconds")]
    public int RetryDelaySeconds { get; init; }

    [JsonPropertyName("secret_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecretKey { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }
}
