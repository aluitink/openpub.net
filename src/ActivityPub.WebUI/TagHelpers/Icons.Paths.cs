namespace ActivityPub.WebUI.TagHelpers;

public static partial class Icons
{
    // Action bar -----------------------------------------------------------

    // Curved reply arrow (was U+21A9).
    public static string Reply => SvgOpen +
        "<path d=\"M9 14 4 9l5-5\"/><path d=\"M4 9h10a6 6 0 0 1 6 6v3\"/>" + SvgClose;

    // Heart (was U+2665).
    public static string Like => SvgOpen +
        "<path d=\"M12 20s-7-4.35-9.5-8.5C1 8.5 2.5 5 6 5c2 0 3.2 1.1 4 2.3C10.8 6.1 12 5 14 5c3.5 0 5 3.5 3.5 6.5C19 15.65 12 20 12 20Z\"/>" + SvgClose;

    // Repeat / boost arrows (was U+21BB).
    public static string Boost => SvgOpen +
        "<path d=\"M4 12a8 8 0 0 1 13.7-5.6L20 8\"/><path d=\"M20 4v4h-4\"/><path d=\"M20 12a8 8 0 0 1-13.7 5.6L4 16\"/><path d=\"M4 20v-4h4\"/>" + SvgClose;

    // Speech bubble (was U+1F4AC).
    public static string Comment => SvgOpen +
        "<path d=\"M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12Z\"/>" + SvgClose;

    // Vertical three-dots "more" (was U+22EE).
    public static string More => SvgOpen +
        "<circle cx=\"12\" cy=\"5\" r=\"1.4\"/><circle cx=\"12\" cy=\"12\" r=\"1.4\"/><circle cx=\"12\" cy=\"19\" r=\"1.4\"/>" + SvgClose;

    // Warning triangle (was U+26A0) — matches the error-page set.
    public static string Warning => SvgOpen +
        "<path d=\"M10.3 4.3 2.5 18a2 2 0 0 0 1.7 3h15.6a2 2 0 0 0 1.7-3L13.7 4.3a2 2 0 0 0-3.4 0Z\"/><path d=\"M12 9v5\"/><path d=\"M12 17h.01\"/>" + SvgClose;

    // Magnifier (was U+2315 / ⌕).
    public static string Search => SvgOpen +
        "<circle cx=\"11\" cy=\"11\" r=\"7\"/><path d=\"m20 20-3.5-3.5\"/>" + SvgClose;

    // Caret / chevron-down (was U+25BE).
    public static string Caret => SvgOpen +
        "<path d=\"m6 9 6 6 6-6\"/>" + SvgClose;

    // Navigation -----------------------------------------------------------

    // Home (was U+2302 ⌂).
    public static string Home => SvgOpen +
        "<path d=\"M4 11 12 4l8 7\"/><path d=\"M6 10v9h12v-9\"/><path d=\"M10 19v-5h4v5\"/>" + SvgClose;

    // Inbox / envelope (was U+2709 ✉).
    public static string Inbox => SvgOpen +
        "<rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><path d=\"m4 7 8 6 8-6\"/>" + SvgClose;

    // Profile / person (was U+1F464 👤).
    public static string Profile => SvgOpen +
        "<circle cx=\"12\" cy=\"8\" r=\"4\"/><path d=\"M4 20a8 8 0 0 1 16 0\"/>" + SvgClose;

    // Lightbox prev / next (were U+2039 / U+203A).
    public static string Prev => SvgOpen +
        "<path d=\"m15 6-6 6 6 6\"/>" + SvgClose;
    public static string Next => SvgOpen +
        "<path d=\"m9 6 6 6-6 6\"/>" + SvgClose;

    // Close (was U+2715 ✕).
    public static string Close => SvgOpen +
        "<path d=\"M6 6l12 12\"/><path d=\"M18 6 6 18\"/>" + SvgClose;

    // Media ----------------------------------------------------------------

    // Music note (was U+266B ♫).
    public static string Audio => SvgOpen +
        "<path d=\"M9 18V6l10-2v12\"/><circle cx=\"6\" cy=\"18\" r=\"3\"/><circle cx=\"16\" cy=\"16\" r=\"3\"/>" + SvgClose;

    // Document / file (was U+1F4C4 📄).
    public static string Doc => SvgOpen +
        "<path d=\"M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9Z\"/><path d=\"M14 3v6h6\"/><path d=\"M8 13h8\"/><path d=\"M8 17h5\"/>" + SvgClose;

    // Video (play in rounded rect) — used for video attachments.
    public static string Video => SvgOpen +
        "<rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><path d=\"m10 9 5 3-5 3Z\"/>" + SvgClose;

    // Link (chain) — used for "copy link".
    public static string Link => SvgOpen +
        "<path d=\"M9 12h6\"/><path d=\"M10 8H8a4 4 0 0 0 0 8h2\"/><path d=\"M14 8h2a4 4 0 0 1 0 8h-2\"/>" + SvgClose;

    // Theme ----------------------------------------------------------------

    // Moon (was U+263E ☾).
    public static string Moon => SvgOpen +
        "<path d=\"M20 14.5A8 8 0 1 1 9.5 4 6.5 6.5 0 0 0 20 14.5Z\"/>" + SvgClose;

    // Sun (was U+2600 ☀).
    public static string Sun => SvgOpen +
        "<circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4\"/>" + SvgClose;

    // Notifications --------------------------------------------------------

    // Plus (was U+2795 ➕) — new follower.
    public static string Plus => SvgOpen +
        "<path d=\"M12 5v14\"/><path d=\"M5 12h14\"/>" + SvgClose;

    // Check (was U+2714 ✔) — follow accepted.
    public static string Check => SvgOpen +
        "<path d=\"m5 12 5 5 9-11\"/>" + SvgClose;

    // Redo / boost notification (was U+1F501 🔁).
    public static string Redo => SvgOpen +
        "<path d=\"M17 2l4 4-4 4\"/><path d=\"M3 12V9a4 4 0 0 1 4-4h14\"/><path d=\"M7 22l-4-4 4-4\"/><path d=\"M21 12v3a4 4 0 0 1-4 4H3\"/>" + SvgClose;

    // Bolt (was U+26A1 ⚡) — generic notification.
    public static string Bolt => SvgOpen +
        "<path d=\"M13 2 4 14h6l-1 8 9-12h-6Z\"/>" + SvgClose;

    // Info (was U+2139 ℹ) — toast info.
    public static string Info => SvgOpen +
        "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 11v5\"/><path d=\"M12 8h.01\"/>" + SvgClose;

    // Palette / utility ----------------------------------------------------

    // Command (was U+2318 ⌘) — palette "communities".
    public static string Cmd => SvgOpen +
        "<path d=\"M9 9V6a3 3 0 1 0-3 3h3Zm0 0v6m0-6h6m-6 6v3a3 3 0 1 1-3-3h3Zm6-6h3a3 3 0 1 0-3-3v3Zm0 6v3a3 3 0 1 0 3-3h-3Zm0 0H9\"/><path d=\"M9 9h6v6H9Z\"/>" + SvgClose;

    // Quote (was U+275D ❝) — palette "notes".
    public static string Quote => SvgOpen +
        "<path d=\"M7 7H4v6h3l-1 4\"/><path d=\"M18 7h-3v6h3l-1 4\"/>" + SvgClose;

    // Clock (was U+23F1 ⏱) — poll duration.
    public static string Clock => SvgOpen +
        "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7v5l3 2\"/>" + SvgClose;
}
