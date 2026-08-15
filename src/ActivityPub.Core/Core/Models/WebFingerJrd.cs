using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a JRD (JSON Resource Descriptor) for WebFinger responses
/// </summary>
public class WebFingerJrd
{
    public string Subject { get; set; } = string.Empty;
    public List<WebFingerLink> Links { get; set; } = new();
}