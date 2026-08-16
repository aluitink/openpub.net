using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a cached WebFinger response
/// </summary>
public class WebFingerResponse
{
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("links")]
    public WebFingerLink[] Links { get; set; } = Array.Empty<WebFingerLink>();

    [JsonPropertyName("cachedAt")]
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}