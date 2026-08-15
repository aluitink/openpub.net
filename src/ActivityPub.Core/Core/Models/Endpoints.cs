using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents the endpoints for an Actor in Activity Streams 2.0
/// </summary>
public record Endpoints
{
    /// <summary>
    /// The proxy URI for the actor
    /// </summary>
    [JsonPropertyName("proxyUrl")]
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// The OAuth authorization endpoint
    /// </summary>
    [JsonPropertyName("oauthAuthorizationEndpoint")]
    public string? OAuthAuthorizationEndpoint { get; set; }

    /// <summary>
    /// The OAuth token endpoint
    /// </summary>
    [JsonPropertyName("oauthTokenEndpoint")]
    public string? OAuthTokenEndpoint { get; set; }

    /// <summary>
    /// The shared inbox endpoint
    /// </summary>
    [JsonPropertyName("sharedInbox")]
    public string? SharedInbox { get; set; }
}