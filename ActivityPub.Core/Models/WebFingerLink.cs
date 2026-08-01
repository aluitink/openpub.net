namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a link in a JRD response
/// </summary>
public class WebFingerLink
{
    public string? Rel { get; set; }
    public string? Type { get; set; }
    public string? Href { get; set; }
}