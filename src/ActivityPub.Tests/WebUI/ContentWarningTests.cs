using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 51.5 — Content-warnings (CW): blur + reveal, per-note and global;
/// respected in the lightbox. Verifies the consistent blur markup, the
/// global "blur sensitive media" preference endpoint, the re-runnable
/// FB.cwInit client hook, the lightbox CW gate, and the supporting CSS.
/// </summary>
public class ContentWarningTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ContentWarningTests(WebUIFactory factory)
    {
        _factory = factory;
    }

    async Task<(HttpClient Client, string Username)> RegisterAndLogin(string username)
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "CW Test" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        return (client, username);
    }

    /// <summary>
    /// Composes a note. A content string prefixed "CW: ..." is recognised by the
    /// timeline controller as both sensitive and carrying a content warning, so
    /// the rendered card carries the CW banner + blur markup.
    /// </summary>
    async Task<string> ComposeNoteAsync(HttpClient client, string content)
    {
        var res = await client.PostAsync("/compose/post", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Content", content },
        }));
        Assert.True(res.IsSuccessStatusCode || res.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"compose failed: {(int)res.StatusCode}");
        return await client.GetStringAsync("/timeline");
    }

    // ---- Per-note CW blur markup (consistent across text + media) ------------

    [Fact]
    public async Task CwNote_Card_RendersBannerAndConsistentBlurMarkup()
    {
        var (client, _) = await RegisterAndLogin($"cw_{Guid.NewGuid().ToString("N")[..8]}");
        var timeline = await ComposeNoteAsync(client, "CW: gore A note that carries a content warning.");

        // The card must expose the CW banner + a re-runnable blur hook.
        Assert.Contains("cw-banner", timeline, StringComparison.Ordinal);
        Assert.Contains("cw-toggle-btn", timeline, StringComparison.Ordinal);

        // The text element is marked data-cw-content so it blurs under the gate.
        // (A text-only CW note has no image grid; the media-consistency rule is
        // covered by the CSS assertions — every [data-cw-content] media type is
        // blurred identically when the card is cw-hidden.)
        Assert.Contains("data-cw-content", timeline, StringComparison.Ordinal);

        // The card root carries the computed data-cw-blur gate. Default pref is
        // "blur on", so a CW note must be data-cw-blur="true".
        Assert.Contains("data-cw-blur=\"true\"", timeline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlainNote_Card_HasNoCwBannerAndBlurGateFalse()
    {
        var (client, _) = await RegisterAndLogin($"cw_{Guid.NewGuid().ToString("N")[..8]}");
        var timeline = await ComposeNoteAsync(client, "Just an ordinary note, nothing sensitive.");

        // A plain note has no banner and its gate is explicitly false so the
        // client does not auto-blur it.
        Assert.DoesNotContain("cw-banner", timeline, StringComparison.Ordinal);
        Assert.Contains("data-cw-blur=\"false\"", timeline, StringComparison.Ordinal);
        Assert.DoesNotContain("data-cw-content", timeline, StringComparison.Ordinal);
    }

    // ---- Global "blur sensitive media" preference ----------------------------

    [Fact]
    public async Task Settings_Index_RendersBlurToggle()
    {
        var (client, _) = await RegisterAndLogin($"cw_{Guid.NewGuid().ToString("N")[..8]}");
        var html = await client.GetStringAsync("/settings");
        Assert.Contains("blurSensitiveMedia", html, StringComparison.Ordinal);
        Assert.Contains("Blur sensitive media", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settings_UpdateDisplay_PersistsFalseAndReflectsInCard()
    {
        var (client, _) = await RegisterAndLogin($"cw_{Guid.NewGuid().ToString("N")[..8]}");

        // Turn the preference OFF.
        var update = await client.PostAsync("/settings/updatedisplay", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "blurSensitiveMedia", "" }, // unchecked checkbox → false
        }));
        Assert.True(update.IsSuccessStatusCode || update.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"settings update failed: {(int)update.StatusCode}");

        // The settings page now renders the checkbox unchecked.
        var settingsHtml = await client.GetStringAsync("/settings");
        Assert.DoesNotContain("checked=\"checked\"", settingsHtml, StringComparison.Ordinal);

        // A newly composed CW note must now render data-cw-blur="false" because
        // the viewer chose not to auto-blur (the banner is still present so the
        // user can blur it manually).
        var timeline = await ComposeNoteAsync(client, "CW: gore A note with a content warning.");
        Assert.Contains("cw-banner", timeline, StringComparison.Ordinal);
        Assert.Contains("data-cw-blur=\"false\"", timeline, StringComparison.Ordinal);
        Assert.DoesNotContain("data-cw-blur=\"true\"", timeline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settings_UpdateDisplay_True_RestoresBlurGate()
    {
        var (client, _) = await RegisterAndLogin($"cw_{Guid.NewGuid().ToString("N")[..8]}");

        // Explicitly set the preference to ON.
        var update = await client.PostAsync("/settings/updatedisplay", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "blurSensitiveMedia", "true" },
        }));
        Assert.True(update.IsSuccessStatusCode || update.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"settings update failed: {(int)update.StatusCode}");

        var settingsHtml = await client.GetStringAsync("/settings");
        Assert.Contains("checked=\"checked\"", settingsHtml, StringComparison.Ordinal);

        var timeline = await ComposeNoteAsync(client, "CW: gore A note with a content warning.");
        Assert.Contains("data-cw-blur=\"true\"", timeline, StringComparison.Ordinal);
    }

    // ---- Card fragment endpoint honours the preference too -------------------

    [Fact]
    public async Task Card_Fragment_ReflectsBlurPreference()
    {
        var (client, _) = await RegisterAndLogin($"cw_{Guid.NewGuid().ToString("N")[..8]}");
        var timeline = await ComposeNoteAsync(client, "CW: gore A note for the card fragment.");

        var idx = timeline.IndexOf("data-activity-id=\"", StringComparison.Ordinal);
        Assert.True(idx >= 0, "timeline should contain a note card");
        idx += "data-activity-id=\"".Length;
        var end = timeline.IndexOf('"', idx);
        var activityId = timeline[idx..end];

        var card = await client.GetStringAsync("/timeline/card/" + Uri.EscapeDataString(activityId));
        Assert.Contains("cw-banner", card, StringComparison.Ordinal);
        // Default (blur on) → the fragment's gate is true.
        Assert.Contains("data-cw-blur=\"true\"", card, StringComparison.Ordinal);
    }

    // ---- Client JS: re-runnable FB.cwInit + lightbox CW gate -----------------

    [Fact]
    public async Task ClientJs_ContainsCwInitAndWiring()
    {
        var js = await (await _factory.CreateClient().GetAsync("/js/app.js")).Content.ReadAsStringAsync();
        Assert.Contains("FB.cwInit", js, StringComparison.Ordinal);
        // Initial blur is driven by the server-computed gate attribute.
        Assert.Contains("data-cw-blur", js, StringComparison.Ordinal);
        // The toggle is idempotent (bound once) so re-init on dynamic inserts
        // does not stack duplicate handlers.
        Assert.Contains("data-cw-bound", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientJs_LiveTimelineAndLoadMore_CallCwInit()
    {
        var js = await (await _factory.CreateClient().GetAsync("/js/app.js")).Content.ReadAsStringAsync();
        // Both dynamic insert paths re-run the CW hook (mirrors linkPreviewInit).
        Assert.Contains("window.FB.cwInit(card)", js, StringComparison.Ordinal);
        Assert.Contains("window.FB.cwInit(frag)", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LightboxJs_RespectsCwGate()
    {
        var js = await (await _factory.CreateClient().GetAsync("/js/lightbox.js")).Content.ReadAsStringAsync();
        // The lightbox detects the source card's cw-hidden state and gates.
        Assert.Contains("cw-hidden", js, StringComparison.Ordinal);
        Assert.Contains("cwGated", js, StringComparison.Ordinal);
        Assert.Contains("revealCw", js, StringComparison.Ordinal);
        Assert.Contains("lightbox-img-blurred", js, StringComparison.Ordinal);
        // Reveal also un-hides the source card in the timeline.
        Assert.Contains("cwSourceCard", js, StringComparison.Ordinal);
    }

    // ---- Layout markup + CSS -------------------------------------------------

    [Fact]
    public async Task Layout_ContainsLightboxCwGateMarkup()
    {
        var (client, _) = await RegisterAndLogin($"cw_{Guid.NewGuid().ToString("N")[..8]}");
        var html = await client.GetStringAsync("/timeline");
        Assert.Contains("lightbox-cw-gate", html, StringComparison.Ordinal);
        Assert.Contains("data-lightbox-cw-reveal", html, StringComparison.Ordinal);
        Assert.Contains("lightbox-cw-reveal", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Css_DefinesCwBlurAndLightboxGateStyles()
    {
        var css = await (await _factory.CreateClient().GetAsync("/css/site.css")).Content.ReadAsStringAsync();
        // Consistent blur of every hidden CW element (images, video, audio).
        Assert.Contains(".note-card.cw-hidden [data-cw-content]", css, StringComparison.Ordinal);
        Assert.Contains(".note-card.cw-hidden [data-cw-content] video", css, StringComparison.Ordinal);
        Assert.Contains(".note-card.cw-hidden [data-cw-content] audio", css, StringComparison.Ordinal);
        // Reveal (un-blur) rule.
        Assert.Contains(".note-card:not(.cw-hidden) [data-cw-content]", css, StringComparison.Ordinal);
        // Dark-theme banner readability.
        Assert.Contains("[data-theme=\"dark\"] .cw-banner", css, StringComparison.Ordinal);
        // Lightbox gate.
        Assert.Contains(".lightbox-img-blurred", css, StringComparison.Ordinal);
        Assert.Contains(".lightbox-cw-gate", css, StringComparison.Ordinal);
        Assert.Contains(".lightbox-cw-reveal", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Css_DefinesSettingsStyles()
    {
        var css = await (await _factory.CreateClient().GetAsync("/css/site.css")).Content.ReadAsStringAsync();
        Assert.Contains(".settings-container {", css, StringComparison.Ordinal);
        Assert.Contains(".settings-toggle", css, StringComparison.Ordinal);
        Assert.Contains(".settings-group", css, StringComparison.Ordinal);
    }

    // ---- Nav: Settings link present ------------------------------------------

    [Fact]
    public async Task Nav_AccountDropdown_ContainsSettingsLink()
    {
        var (client, _) = await RegisterAndLogin($"cw_{Guid.NewGuid().ToString("N")[..8]}");
        var html = await client.GetStringAsync("/timeline");
        Assert.Contains("nav-menu-account", html, StringComparison.Ordinal);
        Assert.Contains("Settings", html, StringComparison.Ordinal);
    }
}
