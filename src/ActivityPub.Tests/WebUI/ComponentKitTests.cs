using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Text.RegularExpressions;
using Xunit;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 49.1 — Component kit extraction + inline-style cleanup.
///
/// Verifies that the WebUI has no per-page &lt;style&gt; blocks and no static
/// inline style="" attributes (the one-off styling that drifts from the shared
/// site.css), that the shared stylesheet actually carries the extracted
/// component-kit primitives, and that the two pages that previously shipped
/// their own &lt;style&gt; blocks (Discover/Suggestions and Federation Health)
/// still render their kit classes from the shared stylesheet.
/// </summary>
public class ComponentKitTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ComponentKitTests(WebUIFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    // ---- Source-tree scan: no inline <style> / static style="" -------------

    static string FindViewsDirectory()
    {
        // Walk up from the test assembly until we find src/ActivityPub.WebUI/Views.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir != null; i++)
        {
            var candidate = new DirectoryInfo(
                Path.Combine(dir.FullName, "ActivityPub.WebUI", "Views"));
            if (candidate.Exists) return candidate.FullName;
            var srcCandidate = new DirectoryInfo(
                Path.Combine(dir.FullName, "src", "ActivityPub.WebUI", "Views"));
            if (srcCandidate.Exists) return srcCandidate.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate ActivityPub.WebUI/Views from " + AppContext.BaseDirectory);
    }

    static (string Path, string Content)[] LoadAllViews()
    {
        var root = FindViewsDirectory();
        return Directory.GetFiles(root, "*.cshtml", SearchOption.AllDirectories)
            .Select(f => (Path: Path.GetRelativePath(root, f), Content: File.ReadAllText(f)))
            .ToArray();
    }

    [Fact]
    public void Views_HaveNoInlineStyleBlocks()
    {
        var offenders = LoadAllViews()
            .Where(v => v.Content.Contains("<style", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Path)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Views_HaveNoStaticInlineStyleAttributes()
    {
        var offenders = new List<string>();
        foreach (var (path, content) in LoadAllViews())
        {
            // Find every style="..." attribute and keep only the static ones.
            var matches = Regex.Matches(content, @"style=""([^""]*)""");
            foreach (Match m in matches)
            {
                var value = m.Groups[1].Value;
                bool isDynamic =
                    value.Trim().Equals("display:none", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("@", StringComparison.Ordinal); // Razor-driven
                if (!isDynamic)
                    offenders.Add($"{path}: style=\"{value}\"");
            }
        }

        Assert.True(offenders.Count == 0, "Static inline style(s) found (move to site.css):\n" + string.Join("\n", offenders));
    }

    // ---- Shared stylesheet carries the component kit -----------------------

    [Fact]
    public async Task SiteCss_DefinesComponentKitPrimitives()
    {
        var client = CreateClient();
        var css = await client.GetStringAsync("/css/site.css");

        // Skeleton row + thin line (extracted from the Timeline/Search skeletons).
        Assert.Contains(".skeleton-row", css);
        Assert.Contains(".skeleton-line.thin", css);

        // Poll options list (extracted from _NoteCard).
        Assert.Contains(".poll-options-list", css);

        // Discover / Suggestions kit (extracted from Suggestions/Index.cshtml).
        Assert.Contains(".discover-container", css);
        Assert.Contains(".suggestions-grid", css);
        Assert.Contains(".suggestion-card", css);
        Assert.Contains(".btn-follow", css);
        Assert.Contains(".filter-keyword", css);
        Assert.Contains(".add-filter-form", css);

        // Federation Health kit (extracted from FederationHealth/Index.cshtml).
        Assert.Contains(".health-status-banner", css);
        Assert.Contains(".status-healthy", css);
        Assert.Contains(".status-critical", css);
        Assert.Contains(".health-card", css);
        Assert.Contains(".health-table", css);
        Assert.Contains(".badge-status", css);
    }

    // ---- Phase 49.2 — spacing/typography token standardization -------------

    [Fact]
    public async Task SiteCss_DefinesLineHeightTokens()
    {
        var client = CreateClient();
        var css = await client.GetStringAsync("/css/site.css");

        Assert.Contains("--lh-none", css);
        Assert.Contains("--lh-tight", css);
        Assert.Contains("--lh-normal", css);
        Assert.Contains("--lh-body", css);
        Assert.Contains("--lh-relaxed", css);
    }

    [Fact]
    public async Task SiteCss_HasNoOneOffLineHeightLiterals()
    {
        var client = CreateClient();
        var css = await client.GetStringAsync("/css/site.css");

        // Every metric line-height must come from the --lh-* tokens. The only
        // permitted raw literal is the deliberate `line-height: 0` layout reset.
        var lines = css.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("line-height:", StringComparison.Ordinal))
            .ToList();

        foreach (var line in lines)
        {
            Assert.True(
                line.Contains("var(--lh-") || line == "line-height: 0;",
                $"One-off line-height literal (should use a --lh-* token): {line}");
        }
    }

    [Fact]
    public async Task SiteCss_HasNoRawRadiusLiteralsThatDuplicateAToken()
    {
        var client = CreateClient();
        var css = await client.GetStringAsync("/css/site.css");

        // 4px == --radius-sm, 8px == --radius-md, 6px == --btn-radius. Any of
        // these written as a raw border-radius literal is a one-off that should
        // reference the token. (Distinct values like 2px/3px/10px/20px/0 and
        // multi-value radii are legitimately different and are allowed.)
        var offenders = new List<string>();
        foreach (var raw in new[] { "border-radius: 4px;", "border-radius: 8px;", "border-radius: 6px;" })
        {
            if (css.Contains(raw, StringComparison.Ordinal))
                offenders.Add(raw);
        }

        Assert.True(offenders.Count == 0, "Raw border-radius literals that duplicate a token:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public async Task SiteCss_HasNoOneOffFontSizeLiterals()
    {
        var client = CreateClient();
        var css = await client.GetStringAsync("/css/site.css");

        // Every font-size must come from the --font-* scale. The only other
        // permitted forms are a token-with-fallback (var(--font-x, …)), a
        // component metric token (--btn-font), or a computed value derived from
        // another token (e.g. the avatar-placeholder glyph sized off
        // --avatar-size). Any raw rem/px/em length is a one-off off the scale.
        var fontSizeRegex = new Regex(@"font-size:\s*([^;]+);");
        var offenders = new List<string>();
        foreach (Match m in fontSizeRegex.Matches(css))
        {
            var value = m.Groups[1].Value.Trim();
            bool ok =
                value.StartsWith("var(--font-", StringComparison.Ordinal) ||
                value.StartsWith("var(--btn-font", StringComparison.Ordinal) ||
                value.StartsWith("calc(", StringComparison.Ordinal);
            if (!ok)
                offenders.Add($"font-size: {value};");
        }

        Assert.True(offenders.Count == 0, "One-off font-size literals (should use the --font-* scale):\n" + string.Join("\n", offenders.Distinct()));
    }

    [Fact]
    public async Task SiteCss_PaddingMarginGap_UseSpaceTokens()
    {
        var client = CreateClient();
        // Strip /* … */ comments first: several carry example snippets (e.g. the
        // Component Kit section documents the inline `display:flex;gap:0.5rem;`
        // the kit replaces) that must not be scanned as live declarations.
        var css = new Regex(@"/\*.*?\*/", RegexOptions.Singleline)
            .Replace(await client.GetStringAsync("/css/site.css"), " ");

        // Every length in a padding/margin/gap declaration (single- or
        // multi-value) must come from the --space-* scale. Permitted raw forms:
        // the zero reset, `auto` (centering), negative lengths (the .sr-only
        // `margin:-1px` overlap + negative positioning hacks), and non-scale
        // units (vh/vw/em/%). var()/calc() are already token-derived.
        var spacingRegex = new Regex(@"(?<![-\w])(padding|margin|gap):\s*([^;{}]+);");
        var offenders = new List<string>();
        foreach (Match m in spacingRegex.Matches(css))
        {
            var prop = m.Groups[1].Value;
            foreach (var token in m.Groups[2].Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (SpacingTokenIsAllowed(token))
                    continue;
                offenders.Add($"{prop}: …{token}…");
            }
        }

        Assert.True(offenders.Count == 0, "One-off padding/margin/gap literals (should use the --space-* scale):\n" + string.Join("\n", offenders.Distinct()));
    }

    private static bool SpacingTokenIsAllowed(string token)
    {
        // Zero reset, centering, or token-derived.
        if (token is "0" or "0px" or "0rem" or "auto")
            return true;
        if (token.StartsWith("var(", StringComparison.Ordinal) ||
            token.StartsWith("calc(", StringComparison.Ordinal))
            return true;
        // Negative lengths are overlap/positioning hacks, not scale spacing.
        if (token.StartsWith("-"))
            return true;
        // Non-scale units stay raw.
        if (token.EndsWith("vh") || token.EndsWith("vw") || token.EndsWith("%") ||
            (token.EndsWith("em") && !token.EndsWith("rem")))
            return true;
        // A plain rem/px length must be a --space-* token, i.e. not a raw length.
        return false;
    }

    // ---- Rendered pages use the kit (no inline style blocks) ---------------

    [Fact]
    public async Task DiscoverPage_UsesComponentKit_AndNoInlineStyle()
    {
        var client = CreateClient();
        var username = $"ck_disc_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterAndLogin(client, username);

        var html = await client.GetStringAsync("/suggestions");

        // Page-level container from the shared kit.
        Assert.Contains("discover-container", html);
        Assert.Contains("page-header", html);
        // The per-page <style> block is gone.
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FederationHealthPage_UsesComponentKit_AndNoInlineStyle()
    {
        var client = CreateClient();
        var username = $"ck_fed_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterAndLogin(client, username);
        await MakeAdmin(username);

        var html = await client.GetStringAsync("/FederationHealth");

        // The status banner + grid come from the shared kit now.
        Assert.Contains("health-status-banner", html);
        Assert.Contains("health-grid", html);
        Assert.Contains("health-card", html);
        // The per-page <style> block is gone.
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
    }

    // ---- helpers -----------------------------------------------------------

    async Task RegisterAndLogin(HttpClient client, string username, string displayName = "Kit Test")
    {
        await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", displayName },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
    }

    async Task MakeAdmin(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (user != null)
        {
            user.IsAdmin = true;
            await db.SaveChangesAsync();
        }
    }

    static FormUrlEncodedContent CreateFormContent(IDictionary<string, string> fields)
        => new(fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value ?? string.Empty)));
}
