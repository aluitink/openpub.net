using ActivityPub.Core.Models;

namespace FederationApp.Federation;

public class WebFingerResponse
{
    public string Subject { get; set; } = string.Empty;
    public List<WebFingerLink> Links { get; set; } = new();
    public Dictionary<string, object>? Aliases { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}

public class WebFingerLink
{
    public string Rel { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string? Template { get; set; }
}
