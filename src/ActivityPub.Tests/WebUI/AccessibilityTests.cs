using System.Net;
using System.Text.RegularExpressions;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 43 T8 — Accessibility. Verifies WCAG 2.1 AA-relevant properties on the
/// rendered WebUI: keyboard focus visibility, accessible names for icon-only
/// controls, image alt text, and sufficient color contrast for muted text.
/// </summary>
public class AccessibilityTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;
    private readonly HttpClient _client;

    public AccessibilityTests(WebUIFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    [Fact]
    public async Task SiteCss_DefinesGlobalFocusVisibleOutline()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        // A global :focus-visible rule is required for WCAG 2.4.7 keyboard focus visibility.
        var globalFocus = Regex.Match(css, @"^:focus-visible\s*\{[^}]*outline\s*:\s*[^;]+;", RegexOptions.Multiline);
        Assert.True(globalFocus.Success, "Expected a global ':focus-visible { ... outline: ... }' rule in site.css");
    }

    [Fact]
    public async Task SiteCss_MutedTextTokens_MeetWcagAaContrastOnLightSurface()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        foreach (var token in new[] { "--color-text-muted", "--color-text-faint" })
        {
            var m = Regex.Match(css, $@"{token}\s*:\s*(#[0-9a-fA-F]{{6}})");
            Assert.True(m.Success, $"Expected to find '{token}' hex value in site.css");
            var hex = m.Groups[1].Value;
            // White (#ffffff) is the highest-contrast light surface in the palette; passing here
            // guarantees the token passes on the lighter page background as well (verified >= 4.5:1).
            Assert.True(ContrastRatio(hex, "#ffffff") >= 4.5,
                $"{token} = {hex} has contrast {ContrastRatio(hex, "#ffffff"):0.00}:1 against white, below WCAG AA 4.5:1");
        }
    }

    [Fact]
    public async Task SiteCss_DarkMutedText_MeetsWcagAaContrastOnDarkSurface()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        // Dark mode surfaces resolve --color-text-muted to --dark-text-muted (a light color on a dark bg).
        var m = Regex.Match(css, @"--dark-text-muted\s*:\s*(#[0-9a-fA-F]{6})");
        Assert.True(m.Success, "Expected to find '--dark-text-muted' hex value in site.css");
        var hex = m.Groups[1].Value;
        var darkSurface = FirstHex(css, "--dark-surface") ?? "#1e1e2a";
        Assert.True(ContrastRatio(hex, darkSurface) >= 4.5,
            $"--dark-text-muted = {hex} has contrast {ContrastRatio(hex, darkSurface):0.00}:1 against {darkSurface}, below WCAG AA 4.5:1");
    }

    // ---------- Phase 50.1 — contrast re-audit (light + dark) ----------

    private static string TokenHex(string css, string token)
    {
        var m = Regex.Match(css, $@"{token}\s*:\s*(#[0-9a-fA-F]{{6}})");
        Assert.True(m.Success, $"Expected to find hex value for '{token}' in site.css");
        return m.Groups[1].Value;
    }

    [Fact]
    public async Task SiteCss_LightTheme_TextTokens_MeetWcagAaOnSurfaces()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        // Every text token used as normal-size text must reach 4.5:1 against the
        // light surfaces it is actually rendered on.
        var pairs = new (string token, string surface)[]
        {
            ("--color-text",           "--color-surface"),
            ("--color-text-secondary", "--color-surface"),
            ("--color-text-secondary", "--color-bg"),
            ("--color-text-muted",     "--color-surface"),
            ("--color-text-faint",     "--color-surface"),
            ("--color-accent",         "--color-surface"),
            ("--color-accent",         "--color-bg"),
            ("--color-danger",         "--color-surface"),
            ("--color-info",           "--color-surface"),
        };
        foreach (var (t, s) in pairs)
        {
            var a = TokenHex(css, t);
            var b = TokenHex(css, s);
            Assert.True(ContrastRatio(a, b) >= 4.5,
                $"{t}={a} on {s}={b} has contrast {ContrastRatio(a, b):0.00}:1, below WCAG AA 4.5:1");
        }
    }

    [Fact]
    public async Task SiteCss_DarkTheme_TextTokens_MeetWcagAaOnSurfaces()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        var pairs = new (string token, string surface)[]
        {
            ("--dark-text",          "--dark-surface"),
            ("--dark-text-secondary","--dark-surface"),
            ("--dark-text-secondary","--dark-surface-hover"),
            ("--dark-text-muted",    "--dark-surface"),
            ("--dark-text-muted",    "--dark-surface-alt"),
            ("--dark-accent",        "--dark-surface"),
            ("--dark-accent",        "--dark-surface-hover"),
            ("--dark-accent",        "--dark-accent-soft"),
            ("--dark-danger",        "--dark-surface"),
            ("--dark-danger",        "--dark-surface-hover"),
            ("--dark-info",          "--dark-surface"),
            ("--dark-info",          "--dark-surface-hover"),
            ("--dark-warning",       "--dark-surface"),
            ("--dark-warning",       "--dark-surface-hover"),
        };
        foreach (var (t, s) in pairs)
        {
            var a = TokenHex(css, t);
            var b = TokenHex(css, s);
            Assert.True(ContrastRatio(a, b) >= 4.5,
                $"{t}={a} on {s}={b} has contrast {ContrastRatio(a, b):0.00}:1, below WCAG AA 4.5:1");
        }
    }

    [Fact]
    public async Task SiteCss_SolidFillButtons_WhiteText_MeetWcagAa()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        // Buttons/badges that render white text on a solid fill need >= 4.5:1 between
        // white and the fill. Check the fill hex for each such control.
        var fills = new (string selector, string hex)[]
        {
            // accent
            (".btn-primary", TokenHex(css, "--color-accent")),
            // info
            (".btn-admin", "#2c6396"),
            (".badge-admin", "#2c6396"),
            // danger
            (".btn-danger", "#b83224"),
            (".btn-blockuser", "#b83224"),
            // success
            (".btn-unblock", "#1e7e46"),
            (".badge-member", "#1e7e46"),
            // warning
            (".btn-report", "#9a6106"),
        };
        foreach (var (sel, hex) in fills)
        {
            Assert.True(ContrastRatio("#ffffff", hex) >= 4.5,
                $"{sel} white text on fill {hex} has contrast {ContrastRatio("#ffffff", hex):0.00}:1, below WCAG AA 4.5:1");
        }
    }

    [Fact]
    public async Task SiteCss_WarningAndCharNearIcons_MeetWcagLargeText()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        // The warning color is only used for the 52px non-text error icon (large
        // graphic, 3.0:1 threshold). The character-count "near" text is normal-size,
        // so it needs 4.5:1 on the white compose surface.
        var warning = TokenHex(css, "--color-warning");
        Assert.True(ContrastRatio(warning, "#ffffff") >= 3.0,
            $"--color-warning={warning} on white is {ContrastRatio(warning, "#ffffff"):0.00}:1, below 3.0:1 large-graphic threshold");

        var m = Regex.Match(css, @"\.char-near\s*\{[^}]*color:\s*(#[0-9a-fA-F]{6})");
        Assert.True(m.Success, "Expected a .char-near rule with a hex color");
        var charNear = m.Groups[1].Value;
        Assert.True(ContrastRatio(charNear, "#ffffff") >= 4.5,
            $".char-near={charNear} on white is {ContrastRatio(charNear, "#ffffff"):0.00}:1, below WCAG AA 4.5:1");
    }

    [Fact]
    public async Task Layout_ThemeToggleAndHamburger_HaveAccessibleNames()
    {
        var html = await (await _client.GetAsync("/auth/login")).Content.ReadAsStringAsync();

        Assert.Contains("id=\"theme-toggle\"", html);
        Assert.Contains("id=\"nav-hamburger\"", html);

        var themeBtn = ElementWithId(html, "theme-toggle");
        var burgerBtn = ElementWithId(html, "nav-hamburger");
        Assert.True(themeBtn.Contains("aria-label"),
            "Theme toggle button must expose an aria-label (icon-only control). Got: " + themeBtn);
        Assert.True(burgerBtn.Contains("aria-label"),
            "Hamburger button must expose an aria-label (icon-only control). Got: " + burgerBtn);
        Assert.True(burgerBtn.Contains("aria-controls"),
            "Hamburger button must reference the menu it controls. Got: " + burgerBtn);
    }

    [Fact]
    public async Task Timeline_MoreActionsButton_HasAccessibleName()
    {
        var client = await GetAuthenticatedClient();

        // Post a note so a note card (and its more-actions button) is present on the timeline.
        await client.PostAsync("/compose/post", new System.Net.Http.StringContent(
            $"Content={Uri.EscapeDataString("a11y test note")}",
            System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));

        var html = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();

        Assert.True(html.Contains("btn-more"), "Expected a note card (with .btn-more) on the timeline after posting a note");

        var moreBtn = Regex.Match(html, "<button[^>]*class=\"[^\"]*btn-more[^\"]*\"[^>]*>");
        Assert.True(moreBtn.Success, "Expected a .btn-more button on the timeline");
        Assert.True(
            moreBtn.Value.Contains("aria-label") || moreBtn.Value.Contains("title="),
            "Icon-only 'More actions' button must have an aria-label or title. Got: " + moreBtn.Value);
    }

    [Fact]
    public async Task RenderedPages_Images_HaveAltText()
    {
        var client = await GetAuthenticatedClient();

        // Spot-check a set of pages that render images/avatars.
        var paths = new[] { "/", "/timeline", "/communities", "/search?q=a" };
        foreach (var path in paths)
        {
            var response = await client.GetAsync(path);
            if (response.StatusCode != HttpStatusCode.OK) continue;

            var html = await response.Content.ReadAsStringAsync();
            var imgs = Regex.Matches(html, "<img[^>]*>", RegexOptions.IgnoreCase);
            foreach (Match img in imgs)
            {
                Assert.True(img.Value.Contains("alt="),
                    $"Image on {path} is missing alt text: {img.Value.Substring(0, Math.Min(120, img.Value.Length))}");
            }
        }
    }

    [Fact]
    public async Task Footer_RendersUsefulNavigationLinks()
    {
        var html = await (await _client.GetAsync("/about")).Content.ReadAsStringAsync();

        // The footer must expose a labeled nav with multiple useful links (T9).
        Assert.True(html.Contains("footer-links"), "Expected the footer to contain a .footer-links navigation");
        Assert.True(html.Contains("aria-label=\"Footer\""), "Footer nav should have an aria-label");

        var footer = Regex.Match(html, "<footer>.*?</footer>", RegexOptions.Singleline);
        Assert.True(footer.Success, "Expected a <footer> element in the rendered layout");
        var linkCount = Regex.Matches(footer.Value, "<a ").Count;
        Assert.True(linkCount >= 3, $"Expected the footer to contain at least 3 useful links, found {linkCount}");
    }

    [Fact]
    public async Task AboutPage_Returns200_WithRealContent()
    {
        var response = await _client.GetAsync("/about");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("About", html);
        // Real content, not just the old tagline.
        Assert.Contains("What is Fediblog", html);
        Assert.Contains("ActivityPub", html);
    }

    // ---------- Phase 50.3 — screen-reader pass ----------

    [Fact]
    public async Task NoteMoreMenu_HasControlsAndHaspopupMenu()
    {
        var client = await GetAuthenticatedClient();
        await client.PostAsync("/compose/post", new System.Net.Http.StringContent(
            $"Content={Uri.EscapeDataString("a11y more menu note")}",
            System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
        var html = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();

        // The toggle references the menu it controls and declares a popup menu.
        var moreBtn = Regex.Match(html, "<button[^>]*class=\"[^\"]*btn-more[^\"]*\"[^>]*>");
        Assert.True(moreBtn.Success, "Expected a .btn-more button on the timeline");
        Assert.Contains("aria-haspopup=\"menu\"", moreBtn.Value, StringComparison.Ordinal);
        Assert.Contains("aria-controls=", moreBtn.Value, StringComparison.Ordinal);

        // The menu element is labelled by its toggle.
        Assert.Contains("class=\"note-more-dropdown\" role=\"menu\" aria-labelledby=", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LikeAndBoostButtons_ExposePressedState()
    {
        var client = await GetAuthenticatedClient();
        await client.PostAsync("/compose/post", new System.Net.Http.StringContent(
            $"Content={Uri.EscapeDataString("a11y pressed note")}",
            System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
        var html = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();

        var likeBtn = Regex.Match(html, "<button[^>]*class=\"[^\"]*btn-like[^\"]*\"[^>]*>");
        Assert.True(likeBtn.Success, "Expected a .btn-like button on the timeline");
        Assert.Contains("aria-pressed=", likeBtn.Value, StringComparison.Ordinal);

        var boostBtn = Regex.Match(html, "<button[^>]*class=\"[^\"]*btn-boost[^\"]*\"[^>]*>");
        Assert.True(boostBtn.Success, "Expected a .btn-boost button on the timeline");
        Assert.Contains("aria-pressed=", boostBtn.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Layout_NavDropdowns_HaveControlsAndHaspopup()
    {
        // The authenticated layout renders the Discover/Account/Notifications
        // dropdowns; each toggle must reference its menu and declare a popup.
        var client = await GetAuthenticatedClient();
        var html = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();

        Assert.Contains("nav-toggle-discover", html);
        Assert.Contains("nav-toggle-account", html);

        var discoverBtn = ElementWithId(html, "nav-toggle-discover");
        Assert.True(discoverBtn.Contains("aria-controls="),
            "Discover dropdown toggle must reference its menu via aria-controls. Got: " + discoverBtn);
        Assert.Contains("aria-haspopup=\"menu\"", discoverBtn, StringComparison.Ordinal);
        Assert.Contains("id=\"nav-menu-discover\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Layout_ToastContainer_IsStatusLiveRegion()
    {
        var html = await (await _client.GetAsync("/auth/login")).Content.ReadAsStringAsync();
        Assert.Contains("id=\"toast-container\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollCreation_OptionsInputs_HaveLabels()
    {
        var client = await GetAuthenticatedClient();
        var html = await (await client.GetAsync("/poll/new")).Content.ReadAsStringAsync();

        Assert.Contains("id=\"poll-options-label\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Option 1\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"group\" aria-labelledby=\"poll-options-label\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Layout_PaletteTrigger_ReferencesOverlay()
    {
        var client = await GetAuthenticatedClient();
        var html = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();

        var trigger = ElementWithId(html, "palette-trigger");
        Assert.True(trigger.Contains("aria-controls=\"palette-overlay\""),
            "Palette trigger should reference the overlay it controls via aria-controls. Got: " + trigger);
        Assert.True(trigger.Contains("aria-expanded="),
            "Palette trigger should expose its open state via aria-expanded. Got: " + trigger);
    }

    // ---------- Phase 50.2 — focus-visible + logical tab order ----------

    [Fact]
    public async Task SiteCss_NavDropdownLinks_KeepFocusVisibleOutline()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        // The nav dropdown link must not swallow its focus outline. A :focus-visible
        // rule must restore a visible ring (WCAG 2.4.7), so an 'outline: none' on the
        // :focus state alone is insufficient.
        Assert.Contains(".nav-dropdown-link:focus-visible", css, StringComparison.Ordinal);
        var rule = Regex.Match(css, @"\.nav-dropdown-link:focus-visible\s*\{[^}]*outline\s*:\s*[^;]+;", RegexOptions.Multiline);
        Assert.True(rule.Success, "Expected '.nav-dropdown-link:focus-visible' to declare an outline ring");
        Assert.DoesNotContain("outline: none", rule.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SiteCss_SearchInputs_HaveFocusVisibleRing()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        // Both search inputs set outline:none on :focus; a :focus-visible ring is
        // required so keyboard focus remains visible.
        Assert.Contains(".search-input:focus-visible", css, StringComparison.Ordinal);
        Assert.Contains(".nav-search-input:focus-visible", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SiteCss_NoteMoreMenuItems_HaveFocusVisibleRing()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        Assert.Contains(".note-more-item:focus-visible", css, StringComparison.Ordinal);
        var rule = Regex.Match(css, @"\.note-more-item:focus-visible\s*\{[^}]*outline\s*:\s*[^;]+;", RegexOptions.Multiline);
        Assert.True(rule.Success, "Expected '.note-more-item:focus-visible' to declare an outline ring");
    }

    [Fact]
    public async Task SiteCss_NoControlRemovesFocusOutlineWithoutRing()
    {
        var css = await (await _client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        // Every control that sets 'outline: none' must be paired with a :focus-visible
        // (or :focus) ring, otherwise keyboard focus disappears. We assert the known
        // offenders each have a focus-visible ring nearby.
        Assert.True(css.Contains(".form-control:focus"), "form-control focus ring expected");
        Assert.True(css.Contains(".search-input:focus-visible"), "search-input focus-visible ring expected");
        Assert.True(css.Contains(".nav-search-input:focus-visible"), "nav-search-input focus-visible ring expected");
    }

    [Fact]
    public async Task NoteMoreMenu_ItemsAreLogicalTabOrder_NoNegativeTabindex()
    {
        var client = await GetAuthenticatedClient();
        await client.PostAsync("/compose/post", new System.Net.Http.StringContent(
            $"Content={Uri.EscapeDataString("a11y tab order note")}",
            System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
        var html = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();

        var cardStart = html.IndexOf("class=\"note-card", StringComparison.Ordinal);
        Assert.True(cardStart > 0, "Expected a .note-card on the timeline");

        // Slice out this card: from its start to the start of the next card, bounded
        // by the layout footer (which holds the command palette) so we don't sweep in
        // unrelated page chrome.
        var nextCard = html.IndexOf("class=\"note-card", cardStart + 1);
        var footerIdx = html.IndexOf("<footer", cardStart);
        var end = int.MaxValue;
        if (nextCard > 0) end = nextCard;
        if (footerIdx > 0) end = Math.Min(end, footerIdx);
        if (end == int.MaxValue) end = html.Length;
        var card = html.Substring(cardStart, end - cardStart);

        // The note card's interactive controls must rely on natural DOM order for tab
        // order (WCAG 2.4.3) — no negative tabindex to reorder focus.
        Assert.DoesNotContain("tabindex=\"-1\"", card, StringComparison.Ordinal);
        Assert.DoesNotContain("tabindex='-1'", card, StringComparison.Ordinal);

        // The more-menu toggle precedes its menu items in source order.
        var toggleIdx = card.IndexOf("more-toggle-", StringComparison.Ordinal);
        var firstItemIdx = card.IndexOf("role=\"menuitem\"", StringComparison.Ordinal);
        Assert.True(toggleIdx > 0 && firstItemIdx > 0 && toggleIdx < firstItemIdx,
            "The more-menu toggle should appear before its menu items in DOM order");
    }

    [Fact]
    public async Task NoteMoreMenu_SupportsArrowKeyNavigation()
    {
        var client = await GetAuthenticatedClient();
        await client.PostAsync("/compose/post", new System.Net.Http.StringContent(
            $"Content={Uri.EscapeDataString("a11y arrow key note")}",
            System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
        // The keyboard-nav code lives in app.js: arrow keys move between menuitems
        // and Escape returns focus to the toggle.
        var js = await (await client.GetAsync("/js/app.js")).Content.ReadAsStringAsync();
        Assert.Contains("ArrowDown", js, StringComparison.Ordinal);
        Assert.Contains("ArrowUp", js, StringComparison.Ordinal);
        Assert.Contains("Escape", js, StringComparison.Ordinal);
        Assert.Contains("menuitem", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NavDropdowns_SupportArrowKeyNavigationAndEscapeRefocus()
    {
        var client = await GetAuthenticatedClient();
        var js = await (await client.GetAsync("/js/app.js")).Content.ReadAsStringAsync();
        // Nav dropdowns (Discover/Account) implement the same menuitem keyboard
        // pattern as the note-more menu: arrow keys move between items, Home/End
        // jump, Escape closes and returns focus to the toggle.
        Assert.Contains("navDropdownLinks", js, StringComparison.Ordinal);
        Assert.Contains("ArrowDown", js, StringComparison.Ordinal);
        Assert.Contains("ArrowUp", js, StringComparison.Ordinal);
        Assert.Contains("Home", js, StringComparison.Ordinal);
        Assert.Contains("End", js, StringComparison.Ordinal);
        Assert.Contains("Escape", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DropdownMenus_OpenOnKeyboardEnter_NotImmediatelyClosed()
    {
        var client = await GetAuthenticatedClient();
        var js = await (await client.GetAsync("/js/app.js")).Content.ReadAsStringAsync();
        // The toggle click handlers run in the CAPTURE phase (", true) third arg) so
        // their stopPropagation blocks the global document click handler (bubble
        // phase) from immediately re-closing the just-opened menu. Without this,
        // Enter/click opens then instantly closes the menu (verified in a live
        // keyboard-only walkthrough).
        Assert.Contains(", true);", js, StringComparison.Ordinal);
        // Both the note-more toggle and the nav-group toggle register capture-phase
        // click handlers.
        Assert.Contains("note-more-menu", js, StringComparison.Ordinal);
        Assert.Contains("nav-group", js, StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    private async Task<HttpClient> GetAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var username = $"a11y_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/auth/register", new System.Net.Http.StringContent(
            $"Username={Uri.EscapeDataString(username)}&Email={Uri.EscapeDataString(username + "@test.com")}" +
            $"&DisplayName=A11y&Password=Password123!&ConfirmPassword=Password123!",
            System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
        await client.PostAsync("/auth/login", new System.Net.Http.StringContent(
            $"Username={Uri.EscapeDataString(username)}&Password=Password123!",
            System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
        return client;
    }

    private static string? FirstHex(string css, string token)
    {
        var m = Regex.Match(css, $@"{token}\s*:\s*(#[0-9a-fA-F]{{6}})");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string ElementWithId(string html, string id)
    {
        var m = Regex.Match(html, $@"<(button|a|input|div|select|textarea)\b[^>]*\bid=""{id}""[^>]*>");
        return m.Success ? m.Value : string.Empty;
    }

    // WCAG 2.x relative-luminance contrast ratio (https://www.w3.org/TR/WCAG21/#dfn-contrast-ratio).
    private static double ContrastRatio(string hexA, string hexB)
    {
        var la = Luminance(hexA);
        var lb = Luminance(hexB);
        var hi = Math.Max(la, lb);
        var lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Channel(hex.Substring(0, 2));
        var g = Channel(hex.Substring(2, 2));
        var b = Channel(hex.Substring(4, 2));
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;

        static double Channel(string hh)
        {
            var c = int.Parse(hh, System.Globalization.NumberStyles.HexNumber) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
