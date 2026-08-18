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
