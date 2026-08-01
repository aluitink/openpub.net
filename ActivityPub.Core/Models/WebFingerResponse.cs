namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a cached WebFinger response
/// </summary>
public class WebFingerResponse
{
    public string Subject { get; set; } = string.Empty;
    public WebFingerLink[] Links { get; set; } = Array.Empty<WebFingerLink>();
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}