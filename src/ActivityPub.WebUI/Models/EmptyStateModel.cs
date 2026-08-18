namespace ActivityPub.WebUI.Models;

/// <summary>
/// Typed model for the shared <c>Shared/_EmptyState</c> partial. One markup
/// pattern for every "no data" moment so no page invents its own empty state.
/// </summary>
public class EmptyStateModel
{
    /// <summary>Short heading for the empty state (required).</summary>
    public string Title { get; init; } = "Nothing here yet";

    /// <summary>Optional line of help / next step under the heading.</summary>
    public string? Hint { get; init; }

    /// <summary>Optional CTA button/link text. Requires <see cref="CtaHref"/>.</summary>
    public string? CtaLabel { get; init; }

    /// <summary>Where the CTA points. Required for the CTA to render.</summary>
    public string? CtaHref { get; init; }

    /// <summary>CTA button class; defaults to <c>btn btn-primary</c>.</summary>
    public string CtaClass { get; init; } = "btn btn-primary";
}
