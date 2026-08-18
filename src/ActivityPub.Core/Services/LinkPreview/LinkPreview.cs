namespace ActivityPub.Core.Services;

/// <summary>
/// A server-fetched preview card for an outbound URL, surfaced to the WebUI as
/// JSON. The preview is built from OpenGraph / Twitter / basic &lt;meta&gt; tags
/// (preferred) or an OEmbed response (fallback). All strings are plain text the
/// UI is responsible for HTML-encoding; URLs are validated absolute http(s).
/// </summary>
public class LinkPreview
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Image { get; set; }
    public string? SiteName { get; set; }
    public string? AuthorName { get; set; }
    /// <summary>"og" (OpenGraph/meta), "oembed", or "basic" (title only).</summary>
    public string Source { get; set; } = "basic";
}
