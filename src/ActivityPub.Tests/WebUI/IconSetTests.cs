using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Net.Http;
using Xunit;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using ActivityPub.WebUI.TagHelpers;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 49.3 — single dependency-free inline-SVG icon set.
/// Verifies (a) the server-side Icons table is well-formed and complete,
/// (b) the Razor views render the SVG set (no stray emoji/glyph characters),
/// and (c) the browser-side FB.icon() mirror exists and stays in sync.
/// </summary>
public class IconSetTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public IconSetTests(WebUIFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    // ---- Server-side Icons table ----------------------------------------

    [Theory]
    [MemberData(nameof(AllIconNames))]
    public void Icons_Get_ReturnsWellFormedSvg_ForEveryIcon(string name)
    {
        var svg = Icons.Get(name);

        Assert.False(string.IsNullOrWhiteSpace(svg), $"icon '{name}' is empty");
        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.Contains("viewBox=\"0 0 24 24\"", svg);
        Assert.Contains("fill=\"none\"", svg);
        Assert.Contains("stroke=\"currentColor\"", svg);
        // Stroke-based, not filled — no stray fill paths that would break theming.
        Assert.Contains("stroke-width", svg);
    }

    [Fact]
    public void Icons_Get_ReturnsEmpty_ForUnknownName()
    {
        Assert.Equal(string.Empty, Icons.Get("definitely-not-a-real-icon"));
        Assert.Equal(string.Empty, Icons.Get(""));
        Assert.Equal(string.Empty, Icons.Get(null!));
    }

    [Fact]
    public void Icons_All_ContainsEveryKnownName()
    {
        // The public All list must cover every name the Get switch handles so
        // docs/tests can enumerate the set. (Guard against a name added to the
        // switch but not to All, or vice-versa.)
        var expected = new[]
        {
            "reply","like","boost","comment","more","warning","search","caret",
            "home","inbox","profile","prev","next","close","audio","doc","moon",
            "sun","plus","check","redo","bolt","info","cmd","quote","clock",
            "video","link",
        };
        Assert.Equal(expected.OrderBy(x => x), Icons.All.OrderBy(x => x));
        // Every name in All must actually render.
        foreach (var n in Icons.All)
            Assert.False(string.IsNullOrWhiteSpace(Icons.Get(n)), $"All lists '{n}' but Get returns empty");
    }

    public static IEnumerable<object[]> AllIconNames() =>
        Icons.All.Select(n => new object[] { n });

    // ---- Rendered views use the SVG set (no stray emoji/glyphs) ----------

    [Fact]
    public async Task Layout_RendersSvgIcons_AndNoStrayGlyphs()
    {
        var client = CreateClient();
        var username = $"ic_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);
        var html = await client.GetStringAsync("/");

        // The shared layout always renders: mobile-bottom-nav icons, palette
        // trigger, lightbox controls, nav carets.
        Assert.Contains("fb-icon", html);
        Assert.Contains("<svg", html);
        // The old glyph characters are gone from the layout.
        Assert.DoesNotContain("&#9906;", html);   // ⌕ palette trigger (old)
        Assert.DoesNotContain("&#9662;", html);   // ▾ carets (old)
        Assert.DoesNotContain("&#8962;", html);   // ⌂ home (old)
        Assert.DoesNotContain("&#9993;", html);   // ✉ inbox (old)
        Assert.DoesNotContain("&#9787;", html);   // 👤 profile (old)
        Assert.DoesNotContain("&#10005;", html);  // ✕ close (old)
        Assert.DoesNotContain("&#10094;", html);  // ‹ prev (old)
        Assert.DoesNotContain("&#10095;", html);  // › next (old)
        Assert.DoesNotContain("&#9888;", html);   // ⚠ cw (old)
    }

    [Fact]
    public async Task Timeline_NoteCard_RendersSvgActionIcons()
    {
        var client = CreateClient();
        var username = $"ic_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);
        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "icon test note" },
        }));

        var html = await client.GetStringAsync("/timeline");

        // Like / boost / reply / more action buttons now render the SVG set.
        Assert.Contains("fb-icon", html);
        Assert.Contains("<svg", html);
        // The old action-bar glyphs are gone.
        Assert.DoesNotContain("💬", html);  // reply (old emoji)
        Assert.DoesNotContain("&#8943;", html); // ⋮ more (old)
        Assert.DoesNotContain("&#9835;", html); // ♫ audio (old)
        Assert.DoesNotContain("&#128196;", html); // 📄 doc (old)
    }

    [Fact]
    public async Task Notifications_RendersSvgNotificationIcons()
    {
        var client = CreateClient();
        var username = $"icn_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);
        var html = await client.GetStringAsync("/notifications");

        // The notification list (or its empty state) must not carry the old
        // glyph entities; when items exist they render via <icon>.
        Assert.DoesNotContain("&#10133;", html); // ➕ follow (old)
        Assert.DoesNotContain("&#10003;", html); // ✔ accept (old)
        Assert.DoesNotContain("&#128264;", html); // 🔁 boost (old)
        Assert.DoesNotContain("&#9889;", html);  // ⚡ default (old)
    }

    // ---- Browser-side mirror stays in sync -------------------------------

    [Fact]
    public async Task AppJs_ExposesFbIcon_WithSameSet()
    {
        var client = CreateClient();
        var js = await client.GetStringAsync("/js/app.js");

        Assert.Contains("icon: function(name)", js);
        Assert.Contains("FB.icons", js);
        // The JS set must cover the same names the server exposes.
        foreach (var name in Icons.All)
            Assert.True(js.Contains($"\n        {name}:"), $"FB.icons missing '{name}'");
        // Old glyph swaps are gone from the JS.
        Assert.DoesNotContain("'☀'", js);
        Assert.DoesNotContain("'☾'", js);
        Assert.DoesNotContain("'♥'", js);
        Assert.DoesNotContain("'↻'", js);
        Assert.DoesNotContain("'✓'", js);
        Assert.DoesNotContain("'✕'", js);
    }

    [Fact]
    public async Task JsFiles_HaveNoStrayIconGlyphs()
    {
        var client = CreateClient();
        var poll = await client.GetStringAsync("/js/poll.js");
        Assert.DoesNotContain("⏱", poll);

        var palette = await client.GetStringAsync("/js/palette.js");
        Assert.DoesNotContain("⌘", palette);
        Assert.DoesNotContain("❝", palette);
    }

    // ---- helpers ---------------------------------------------------------

    async Task RegisterUser(HttpClient client, string username, string displayName = "Icon Test")
    {
        await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", displayName },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
    }

    async Task LoginUser(HttpClient client, string username)
    {
        await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
    }

    static FormUrlEncodedContent CreateFormContent(IDictionary<string, string> fields)
    {
        var form = new FormUrlEncodedContent(fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value ?? string.Empty)));
        return form;
    }
}
