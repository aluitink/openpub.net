using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ActivityPub.WebUI.TagHelpers;

/// <summary>
/// Renders a single, dependency-free inline-SVG icon from the app's one icon
/// set, replacing the previous mix of emoji / Unicode glyphs. Usage:
/// &lt;icon name="like" class="note-action-icon" /&gt;
///
/// Every icon shares the same contract: a 24x24 viewBox, stroke-based
/// (fill="none" stroke="currentColor"), round caps/joins, and aria-hidden so
/// it never duplicates a neighbouring text label. Size and colour are driven
/// entirely by CSS on the host element (width/height + currentColor), so the
/// set is theme-aware for free.
///
/// The emitted element is a &lt;span class="fb-icon {author class}"&gt; so the
/// author's own sizing/colour class still applies; fb-icon only guarantees
/// the inner svg scales to 1em and inherits currentColor.
/// </summary>
[HtmlTargetElement("icon")]
public class IconTagHelper : TagHelper
{
    /// <summary>Icon name, e.g. "like", "boost", "search". Must be a known key in <see cref="Icons"/>.</summary>
    [HtmlAttributeName]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional inline SVG (advanced override); ignored when empty.</summary>
    [HtmlAttributeName("svg")]
    public string? Svg { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var inner = !string.IsNullOrWhiteSpace(Svg) ? Svg! : Icons.Get(Name);
        var authorClass = output.Attributes["class"]?.Value?.ToString();

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("aria-hidden", "true");
        output.Attributes.SetAttribute(
            "class",
            string.IsNullOrWhiteSpace(authorClass) ? "fb-icon" : $"fb-icon {authorClass}");
        output.Content.SetHtmlContent(inner);
    }
}
