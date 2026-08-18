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

        // Every length in a padding/margin/gap declaration (shorthand or
        // longhand, single- or multi-value) must come from the --space-* scale.
        // Permitted raw forms: the zero reset, `auto` (centering), negative
        // lengths (the .sr-only `margin:-1px` overlap + negative positioning
        // hacks), non-scale units (vh/vw/em/%), and fine sub-4px pixel nudges
        // (e.g. `margin-top:1px`) that are alignment, not rhythm. var()/calc()
        // are already token-derived.
        var spacingRegex = new Regex(@"(?<![-\w])((?:padding|margin|gap|(?:margin|padding)-(?:top|right|bottom|left))):\s*([^;{}]+);");
        var offenders = new List<string>();
        foreach (Match m in spacingRegex.Matches(css))
        {
            var prop = m.Groups[1].Value;
            // Paren-aware split so function calls (e.g. `env(safe-area-inset-bottom, 0)`)
            // stay a single token instead of breaking at the space inside the parens.
            foreach (var token in SplitSpacingTokens(m.Groups[2].Value))
            {
                if (SpacingTokenIsAllowed(token))
                    continue;
                offenders.Add($"{prop}: …{token}…");
            }
        }

        Assert.True(offenders.Count == 0, "One-off padding/margin/gap literals (should use the --space-* scale):\n" + string.Join("\n", offenders.Distinct()));
    }

    private static IEnumerable<string> SplitSpacingTokens(string value)
    {
        var current = new System.Text.StringBuilder();
        var depth = 0;
        foreach (var c in value)
        {
            if (c == '(') depth++;
            else if (c == ')') depth--;
            if ((c == ' ' || c == '\t') && depth == 0)
            {
                if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0) yield return current.ToString();
    }

    private static bool SpacingTokenIsAllowed(string token)
    {
        // Zero reset, centering, or token-derived.
        if (token is "0" or "0px" or "0rem" or "auto")
            return true;
        if (token.StartsWith("var(", StringComparison.Ordinal) ||
            token.StartsWith("calc(", StringComparison.Ordinal) ||
            token.StartsWith("env(", StringComparison.Ordinal))
            return true;
        // Negative lengths are overlap/positioning hacks, not scale spacing.
        if (token.StartsWith("-"))
            return true;
        // Non-scale units stay raw.
        if (token.EndsWith("vh") || token.EndsWith("vw") || token.EndsWith("%") ||
            (token.EndsWith("em") && !token.EndsWith("rem")))
            return true;
        // Fine sub-4px pixel nudges are alignment, not rhythm (the grid is rem).
        if (token.EndsWith("px") && decimal.TryParse(token[..^2], out var px) && px % 4m != 0)
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

    // ---- Phase 49 Item 4 — empty states, skeletons, loading affordances -----

    [Fact]
    public async Task SiteCss_SkeletonUsesThemeTokens_AndHasSpinner()
    {
        var client = CreateClient();
        var css = await client.GetStringAsync("/css/site.css");

        // The shimmer is now driven by theme tokens, not hard-coded light greys,
        // so it reads correctly in both light and dark.
        Assert.Contains("var(--skeleton-base)", css);
        Assert.Contains("var(--skeleton-shine)", css);
        Assert.Contains("--skeleton-base:", css);
        Assert.Contains("--skeleton-shine:", css);
        // The old hard-coded light greys are gone from the shimmer.
        Assert.DoesNotContain("#e0e0e0 25%, #f0f0f0", css);

        // Dark-mode overrides for the skeleton tokens exist.
        Assert.Contains("--skeleton-base: #2a2a3c", css);
        Assert.Contains("--skeleton-shine: #34344a", css);

        // The shared loading affordance: a spinner + its keyframes.
        Assert.Contains(".loading-spinner", css);
        Assert.Contains("@keyframes fb-spin", css);
        Assert.Contains(".loading-indicator", css);
    }

    [Fact]
    public void SharedEmptyState_AndSkeletonPartials_Exist()
    {
        var views = LoadAllViews();
        var byName = views.ToDictionary(
            v => v.Path.Replace('\\', '/').ToLowerInvariant(),
            v => v.Content);

        Assert.True(byName.ContainsKey("shared/_emptystate.cshtml"),
            "Missing shared partial Views/Shared/_EmptyState.cshtml");
        Assert.True(byName.ContainsKey("shared/_skeletonnotecard.cshtml"),
            "Missing shared partial Views/Shared/_SkeletonNoteCard.cshtml");

        // The empty-state partial renders a labelled, accessible status region.
        var empty = byName["shared/_emptystate.cshtml"];
        Assert.Contains("empty-state-title", empty);
        Assert.Contains("role=\"status\"", empty);
        Assert.Contains("@model EmptyStateModel", empty);

        // The skeleton partial reuses the shared shimmer kit.
        var skeleton = byName["shared/_skeletonnotecard.cshtml"];
        Assert.Contains("skeleton-note-card", skeleton);
        Assert.Contains("skeleton-avatar", skeleton);
    }

    [Fact]
    public void Views_UseSharedEmptyStateAndSkeletonPartials()
    {
        var views = LoadAllViews();
        var byPath = views.ToDictionary(
            v => v.Path.Replace('\\', '/').ToLowerInvariant(),
            v => v.Content);

        // The load-more skeletons were de-duplicated onto the shared partial.
        var timeline = byPath["timeline/index.cshtml"];
        var search = byPath["search/index.cshtml"];
        Assert.Contains("_SkeletonNoteCard", timeline);
        Assert.Contains("_SkeletonNoteCard", search);
        // No more copy-pasted inline skeleton markup in those pages.
        Assert.DoesNotContain("skeleton-avatar", timeline);
        Assert.DoesNotContain("skeleton-avatar", search);

        // The admin tables adopt the shared empty state for their no-data case.
        Assert.Contains("_EmptyState", byPath["admin/users.cshtml"]);
        Assert.Contains("_EmptyState", byPath["admin/dashboard.cshtml"]);
    }

    [Fact]
    public void Views_HaveNoBespokeEmptyStateClasses()
    {
        // Iteration 2 routed every bespoke "no data" block onto the shared
        // _EmptyState partial. The one-off classes below are retired and must not
        // reappear in any view (the Search *prompt* keeps .search-empty*, which is
        // a "start here" hero, not a data empty-state).
        var retired = new[]
        {
            "empty-timeline",
            "empty-notifications",
            "hashtag-empty",
            "no-data",
        };

        var offenders = new List<string>();
        foreach (var (path, content) in LoadAllViews())
        {
            foreach (var cls in retired)
            {
                // Match the class token, not a substring of a longer identifier.
                var re = new Regex($@"class=""[^""]*\b{Regex.Escape(cls)}\b[^""]*""",
                    RegexOptions.IgnoreCase);
                if (re.IsMatch(content))
                    offenders.Add($"{path}: '{cls}'");
            }
        }

        Assert.True(offenders.Count == 0,
            "Bespoke empty-state class(es) found (use the shared _EmptyState partial):\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void DataBearingPages_RouteEmptyStateThroughSharedPartial()
    {
        var views = LoadAllViews();
        var byPath = views.ToDictionary(
            v => v.Path.Replace('\\', '/').ToLowerInvariant(),
            v => v.Content);

        // Every data-bearing page that has a "no data" moment now renders the
        // shared _EmptyState partial (accessible, labelled, one markup pattern).
        var expected = new[]
        {
            "notifications/index.cshtml",
            "follow/following.cshtml",
            "follow/followers.cshtml",
            "hashtag/index.cshtml",
            "trends/index.cshtml",
            "admin/moderation.cshtml",
            "admin/auditlog.cshtml",
            "admin/reports.cshtml",
            "admin/users.cshtml",
            "admin/dashboard.cshtml",
            "federationhealth/index.cshtml",
            "suggestions/index.cshtml",
            "communities/show.cshtml",
            "communities/mycommunities.cshtml",
            "communities/search.cshtml",
            "communities/index.cshtml",
            "search/index.cshtml",
            "timeline/index.cshtml",
        };

        foreach (var page in expected)
        {
            Assert.True(byPath.TryGetValue(page, out var content), $"Missing view {page}");
            Assert.True(content.Contains("_EmptyState", StringComparison.Ordinal),
                $"{page} should render its empty state via the shared _EmptyState partial");
        }
    }

    [Fact]
    public void SearchLoadingUsesSharedSpinnerAffordance()
    {
        var views = LoadAllViews();
        var byPath = views.ToDictionary(
            v => v.Path.Replace('\\', '/').ToLowerInvariant(),
            v => v.Content);

        var search = byPath["search/index.cshtml"];
        // The "Searching…" indicator adopts the shared spinner kit + aria-busy.
        Assert.Contains("loading-indicator", search);
        Assert.Contains("loading-spinner", search);
        Assert.Contains("aria-busy", search);
        // The old bespoke .search-loading CLASS is gone (the element id may remain
        // for the JS hook, but it no longer carries bespoke styling).
        var re = new Regex(@"class=""[^""]*\bsearch-loading\b[^""]*""", RegexOptions.IgnoreCase);
        Assert.True(!re.IsMatch(search), "Bespoke .search-loading class should be retired");
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
