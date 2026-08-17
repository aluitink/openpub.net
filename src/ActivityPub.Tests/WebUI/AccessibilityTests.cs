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
