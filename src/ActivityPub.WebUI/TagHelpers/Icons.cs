namespace ActivityPub.WebUI.TagHelpers;

/// <summary>
/// The app's single, dependency-free inline-SVG icon set. Every entry is a
/// 24x24, stroke-based glyph (fill="none" stroke="currentColor", round
/// caps/joins) so it inherits colour from CSS <c>currentColor</c> and scales
/// to its container — no emoji, no icon font, no third-party dependency.
///
/// The same strings are mirrored in the browser by <c>FB.icon(name)</c> in
/// wwwroot/js/app.js so client-side swaps (optimistic like/boost, toasts,
/// palette) stay byte-for-byte consistent with the server-rendered set.
/// </summary>
public static partial class Icons
{
    /// <summary>
    /// Returns the inline SVG for <paramref name="name"/>, or an empty string
    /// for an unknown name (fail-soft: an unrecognised icon renders nothing
    /// rather than a missing-glyph box).
    /// </summary>
    public static string Get(string name) =>
        name switch
        {
            "reply" => Reply,
            "like" => Like,
            "boost" => Boost,
            "comment" => Comment,
            "more" => More,
            "warning" => Warning,
            "search" => Search,
            "caret" => Caret,
            "home" => Home,
            "inbox" => Inbox,
            "profile" => Profile,
            "prev" => Prev,
            "next" => Next,
            "close" => Close,
            "audio" => Audio,
            "doc" => Doc,
            "moon" => Moon,
            "sun" => Sun,
            "plus" => Plus,
            "check" => Check,
            "redo" => Redo,
            "bolt" => Bolt,
            "info" => Info,
            "cmd" => Cmd,
            "quote" => Quote,
            "clock" => Clock,
            "video" => Video,
            "link" => Link,
            _ => string.Empty,
        };

    /// <summary>Names of every icon in the set (used by tests + docs).</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        "reply","like","boost","comment","more","warning","search","caret",
        "home","inbox","profile","prev","next","close","audio","doc","moon",
        "sun","plus","check","redo","bolt","info","cmd","quote","clock",
        "video","link",
    };

    private const string SvgOpen =
        "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\">";
    private const string SvgClose = "</svg>";
}
