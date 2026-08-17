using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 46 — UI Performance &amp; Perceived Speed. Verifies the client-side
/// pagination/infinite-scroll wiring on the Timeline (the server already
/// paginates via ?page=, so the client fetches the next page and appends it
/// without a full navigation) and the reduced-motion accommodation in the
/// stylesheet. Large timelines are seeded directly through the repository to
/// avoid the per-IP /compose/post rate limit that applies in the test host.
/// </summary>
public class Phase46PerformanceTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public Phase46PerformanceTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    /// <summary>Fetches the external app.js module so tests can assert on client-side behaviour that now lives in a separate file.</summary>
    async Task<string> GetAppJsAsync()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/js/app.js");
        return await res.Content.ReadAsStringAsync();
    }

    /// <summary>Registers + logs in a fresh user and returns their HTTP client.</summary>
    async Task<(HttpClient Client, string Username)> GetAuthenticatedUser()
    {
        var client = _factory.CreateClient();
        var username = $"p46_{Guid.NewGuid().ToString("N")[..8]}";
        var register = await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Test" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(register.IsSuccessStatusCode || register.Headers.Location != null,
            $"register failed: {(int)register.StatusCode}");
        var login = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        Assert.True(login.IsSuccessStatusCode || login.Headers.Location != null,
            $"login failed: {(int)login.StatusCode}");
        return (client, username);
    }

    /// <summary>Posts a single note through the real /compose/post endpoint.</summary>
    async Task PostViaEndpointAsync(HttpClient client, string content)
    {
        var res = await client.PostAsync("/compose/post", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Content", content },
        }));
        Assert.True(res.IsSuccessStatusCode || res.Headers.Location != null,
            $"post failed: {(int)res.StatusCode}");
    }

    /// <summary>Seeds N public notes for a user directly via the repository.</summary>
    async Task SeedNotesAsync(string username, int count, string prefix)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var actor = await repo.GetUserActorAsync(username);
        Assert.NotNull(actor);

        for (var i = 0; i < count; i++)
        {
            var now = DateTime.UtcNow;
            var noteId = $"https://localhost/users/{username}/notes/{Guid.NewGuid():N}";
            var activityId = $"https://localhost/users/{username}/activities/{Guid.NewGuid():N}";
            var note = new Note
            {
                Id = noteId,
                Type = "Note",
                Content = $"{prefix}_{i}_{Guid.NewGuid().ToString("N")[..6]}",
                AttributedTo = actor!.Id,
                Published = now,
                To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
            };
            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actor.Id,
                Object = note,
                Published = now,
                To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
            };
            await repo.SaveActivityAsync(activity);
        }
    }

    [Fact]
    public async Task Timeline_NoteCards_CarryActivityIdForIncrementalRender()
    {
        var (client, username) = await GetAuthenticatedUser();
        var marker = $"p46id_{Guid.NewGuid().ToString("N")[..8]}";
        await SeedNotesAsync(username, 3, marker);

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.True(body.Contains(marker), "Seeded notes should appear in the timeline.");

        var m = Regex.Match(body, @"<div class=""note-card[^>]*?data-activity-id=""([^""]+)""");
        Assert.True(m.Success,
            "Expected each note card to carry a data-activity-id attribute so the client can de-duplicate when appending pages.");
        Assert.False(string.IsNullOrEmpty(m.Groups[1].Value));
    }

    [Fact]
    public async Task Timeline_FullPage_ExposesClientSideLoadMoreWiring()
    {
        var (client, username) = await GetAuthenticatedUser();
        await SeedNotesAsync(username, 25, "p46full");

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        var appJs = await GetAppJsAsync();

        Assert.True(body.Contains("load-more-container"),
            "A full first page (>= 20 notes) should surface the load-more affordance.");
        Assert.True(body.Contains("load-more-btn"), "Expected the load-more button element.");
        Assert.True(body.Contains("data-next"),
            "The load-more container should expose a data-next cursor URL for client-side fetching.");
        Assert.True(appJs.Contains("IntersectionObserver"),
            "The timeline script should set up an IntersectionObserver for infinite scroll.");
        Assert.True(appJs.Contains("load-more-skeleton"),
            "A loading skeleton should be present for perceived speed during incremental loads.");
    }

    [Fact]
    public async Task Timeline_FetchNextPage_ReturnsCardsAndUpdatedCursor()
    {
        var (client, username) = await GetAuthenticatedUser();
        await SeedNotesAsync(username, 45, "p46next");

        var body1 = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        var next1 = Regex.Match(body1, @"data-next=""(?<url>[^""]+)""");
        Assert.True(next1.Success, "First full page should expose a data-next cursor.");

        var url = next1.Groups["url"].Value;
        var abs = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"{client.BaseAddress}{url.TrimStart('/')}";
        var res = await client.GetAsync(new Uri(abs, UriKind.Absolute));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body2 = await res.Content.ReadAsStringAsync();

        Assert.True(body2.Contains("note-card"), "The fetched next page should contain note cards to append.");
        Assert.True(body2.Contains("data-next"),
            "The fetched page 2 (45 notes) is still full, so it should expose the next cursor.");
    }

    [Fact]
    public async Task Timeline_LastPage_HidesLoadMore()
    {
        var (client, username) = await GetAuthenticatedUser();
        await SeedNotesAsync(username, 25, "p46last");

        // Page 2 of 25 notes: only 5 remain, so the page is short and no cursor is emitted.
        var page2 = await (await client.GetAsync("/timeline?page=2")).Content.ReadAsStringAsync();

        Assert.True(page2.Contains("note-card"), "Page 2 should still contain the remaining notes.");
        Assert.False(page2.Contains("class=\"btn btn-secondary load-more-btn\""),
            "The last (partial) page should not render the load-more button element (the client script alone does not count).");
    }

    [Fact]
    public async Task SiteCss_HonorsReducedMotionPreference()
    {
        var css = await (await _factory.CreateClient().GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        var block = Regex.Match(css,
            @"@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{(.*?)\n\}",
            RegexOptions.Singleline);
        Assert.True(block.Success,
            "Expected an @media (prefers-reduced-motion: reduce) block in site.css to disable animation for users who request it.");

        Assert.Contains("animation-duration", block.Groups[1].Value);
        Assert.Contains("transition-duration", block.Groups[1].Value);
        Assert.Contains("0.01ms", block.Groups[1].Value);
    }

    [Fact]
    public async Task Search_Notes_PaginatesAndExposesLoadMore()
    {
        var (client, username) = await GetAuthenticatedUser();
        var marker = $"p46srch_{Guid.NewGuid().ToString("N")[..6]}";

        // 25 matching notes: page 1 is full (20) with a cursor, page 2 is short (5) without one.
        await SeedNotesAsync(username, 25, marker);

        var body1 = await (await client.GetAsync($"/search?q={marker}&tab=notes")).Content.ReadAsStringAsync();
        Assert.True(body1.Contains("search-note-card"), "Search should return the matching notes.");
        Assert.True(body1.Contains("data-next"), "A full first page should expose a data-next cursor.");
        Assert.True(body1.Contains("load-more-btn"), "A full first page should surface the load-more affordance.");

        var next1 = Regex.Match(body1, @"data-next=""(?<url>[^""]+)""");
        Assert.True(next1.Success, "Expected a data-next cursor URL.");
        // Razor HTML-encodes attribute values, so the query string uses &amp;.
        var url = System.Net.WebUtility.HtmlDecode(next1.Groups["url"].Value);
        var abs = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"{client.BaseAddress}{url.TrimStart('/')}";
        var res = await client.GetAsync(new Uri(abs, UriKind.Absolute));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body2 = await res.Content.ReadAsStringAsync();

        Assert.True(body2.Contains("search-note-card"), "Page 2 should contain the remaining notes.");
        Assert.False(body2.Contains("class=\"btn btn-secondary load-more-btn\""),
            "The last (partial) search page should not render the load-more button.");
    }

    [Fact]
    public async Task Timeline_LikeBoostInteraction_SkipsFullNavigation()
    {
        var (client, _) = await GetAuthenticatedUser();
        await PostViaEndpointAsync(client, $"p46xh_{Guid.NewGuid().ToString("N")[..8]}");

        var appJs = await GetAppJsAsync();

        Assert.True(appJs.Contains("e.preventDefault();"),
            "Like/boost/delete should prevent full form navigation (fetch-based) for perceived speed.");
        Assert.True(appJs.Contains("fetch(form.action"),
            "Interactions should use fetch so the page does not reload.");
    }

    /// <summary>Seeds one note carrying an Image attachment (for .note-image assertions).</summary>
    async Task SeedNoteWithImageAsync(string username, string marker)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var actor = await repo.GetUserActorAsync(username);
        Assert.NotNull(actor);

        var noteId = $"https://localhost/users/{username}/notes/{Guid.NewGuid():N}";
        var activityId = $"https://localhost/users/{username}/activities/{Guid.NewGuid():N}";
        var note = new Note
        {
            Id = noteId,
            Type = "Note",
            Content = $"{marker}_with_image",
            AttributedTo = actor!.Id,
            Published = DateTime.UtcNow,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
            Attachment = new List<object>
            {
                JsonSerializer.SerializeToElement(new Dictionary<string, string>
                {
                    { "type", "Image" },
                    { "url", $"https://localhost/media/{Guid.NewGuid():N}.jpg" },
                    { "mediaType", "image/jpeg" },
                }),
            }
        };
        var activity = new Activity
        {
            Id = activityId,
            Type = "Create",
            Actor = actor.Id,
            Object = note,
            Published = DateTime.UtcNow,
            To = new List<string> { "https://www.w3.org/ns/activitystreams#Public" }
        };
        await repo.SaveActivityAsync(activity);
    }

    [Fact]
    public async Task NoteImages_LazyLoadWithReservedBox_NoLayoutShift()
    {
        var (client, username) = await GetAuthenticatedUser();
        await SeedNotesAsync(username, 3, "p46img");
        await SeedNoteWithImageAsync(username, "p46img");

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();

        // Note avatars and attached images should be lazy + async-decoded and
        // carry explicit dimensions so the browser reserves space (no CLS).
        // (Attribute order in the emitted tag is not guaranteed, so assert per-tag.)
        // Attached images should be lazy + async-decoded so they don't block
        // the initial paint; explicit dimensions let the browser reserve space.
        var noteImageTag = Regex.Matches(body, "<img[^>]*>")
            .Cast<Match>().Select(m => m.Value)
            .FirstOrDefault(t => t.Contains("class=\"note-image\""));
        Assert.False(string.IsNullOrEmpty(noteImageTag),
            "Expected the seeded note's image to render as a .note-image <img>.");
        Assert.True(noteImageTag!.Contains("loading=\"lazy\""), "Note images should lazy-load.");
        Assert.True(noteImageTag.Contains("decoding=\"async\""), "Note images should decode async.");

        // The stylesheet should reserve a stable box for attached images so the
        // lazy load never shifts the layout below it.
        var css = await (await client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();
        var block = Regex.Match(css, @"\.note-attachment\s*\{[^}]*\}", RegexOptions.Singleline);
        Assert.True(block.Success, "Expected a .note-attachment rule in site.css.");
        Assert.Contains("min-height", block.Groups[0].Value);
        var imgBlock = Regex.Match(css, @"\.note-image\s*\{[^}]*\}", RegexOptions.Singleline);
        Assert.True(imgBlock.Success, "Expected a .note-image rule in site.css.");
        Assert.Contains("aspect-ratio", imgBlock.Groups[0].Value);
    }

    // ---------------------------------------------------------------------
    // Phase 46.5 — JS module loader. All UI behaviour now ships through the
    // small FB module loader (wwwroot/js/app.js + feature modules) instead of
    // per-view inline scripts; the SignalR bootstrap lives in exactly one
    // place (app.js) and is gated by the CDN script tag in the layout.
    // ---------------------------------------------------------------------

    private async Task<string> JsAsync(string path)
        => await (await _factory.CreateClient().GetAsync(path)).Content.ReadAsStringAsync();

    [Fact]
    public async Task ModuleLoader_ServesAllFeatureModules()
    {
        foreach (var p in new[] { "/js/app.js", "/js/menu.js", "/js/theme.js", "/js/compose.js", "/js/poll.js", "/js/search.js", "/js/suggestions.js" })
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync(p);
            Assert.True(response.IsSuccessStatusCode, $"{p} should be served as a static asset.");
        }
    }

    [Fact]
    public async Task AppJs_DefinesModuleLoaderAndCoreModules()
    {
        var app = await JsAsync("/js/app.js");
        Assert.Contains("window.FB = FB", app, StringComparison.Ordinal);
        Assert.Contains("FB.register", app, StringComparison.Ordinal);
        foreach (var m in new[] { "toast", "theme", "shortcuts", "signals", "timestamps", "loadmore", "notes", "dropdowns" })
            Assert.Contains($"register('{m}'", app, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FeatureModules_RegisterThroughLoader()
    {
        var menu = await JsAsync("/js/menu.js");
        Assert.Contains("FB.register('menu'", menu, StringComparison.Ordinal);
        var compose = await JsAsync("/js/compose.js");
        Assert.Contains("FB.register('compose'", compose, StringComparison.Ordinal);
        var poll = await JsAsync("/js/poll.js");
        Assert.Contains("FB.register('poll'", poll, StringComparison.Ordinal);
        var search = await JsAsync("/js/search.js");
        Assert.Contains("FB.register('search'", search, StringComparison.Ordinal);
        var suggestions = await JsAsync("/js/suggestions.js");
        Assert.Contains("FB.register('suggestions'", suggestions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pages_LoadSingleLoader_PlusFeatureModules_Only()
    {
        var (client, _) = await GetAuthenticatedUser();

        var timeline = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.Contains("js/app.js", timeline, StringComparison.Ordinal);
        Assert.Contains("js/menu.js", timeline, StringComparison.Ordinal);
        // The timeline behaviour (like/boost, load more, more-menu, cw toggle)
        // must come from the loader, not from a page-local inline <script>.
        Assert.DoesNotContain("<script>", timeline, StringComparison.Ordinal);

        var compose = await (await client.GetAsync("/compose")).Content.ReadAsStringAsync();
        Assert.Contains("js/compose.js", compose, StringComparison.Ordinal);
        Assert.Contains("js/app.js", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", compose, StringComparison.Ordinal);

        var search = await (await client.GetAsync("/search?q=module")).Content.ReadAsStringAsync();
        Assert.Contains("js/search.js", search, StringComparison.Ordinal);
        Assert.Contains("js/app.js", search, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", search, StringComparison.Ordinal);

        var poll = await (await client.GetAsync("/poll/new")).Content.ReadAsStringAsync();
        Assert.Contains("js/poll.js", poll, StringComparison.Ordinal);
        Assert.Contains("js/app.js", poll, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", poll, StringComparison.Ordinal);

        var suggestions = await (await client.GetAsync("/suggestions")).Content.ReadAsStringAsync();
        Assert.Contains("js/suggestions.js", suggestions, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", suggestions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignalR_Bootstrap_IsDedupedInLayout()
    {
        var (client, _) = await GetAuthenticatedUser();
        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();

        // One CDN tag per page...
        var signalrTagCount = System.Text.RegularExpressions.Regex.Matches(body, "cdn\\.jsdelivr\\.net[^\"']*signalr", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        Assert.True(signalrTagCount == 1, "The SignalR CDN script should be referenced exactly once (deduped in the layout).");
        // ...and the only connection bootstrap is the one in app.js.
        var app = await JsAsync("/js/app.js");
        var hubBuilderCount = System.Text.RegularExpressions.Regex.Matches(app, "HubConnectionBuilder").Count;
        Assert.True(hubBuilderCount == 1, "The SignalR connection should be built exactly once (deduped in app.js).");
    }

    [Fact]
    public async Task ThemeBootstrap_IsExternalScript()
    {
        var body = await (await _factory.CreateClient().GetAsync("/")).Content.ReadAsStringAsync();
        Assert.Contains("js/theme.js", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeaderImages_UseFetchPriorityHigh()
    {
        var (client, username) = await GetAuthenticatedUser();

        // Give the actor an icon so the profile renders an <img> (not the placeholder).
        var editPage = await (await client.GetAsync("/Profile/Edit")).Content.ReadAsStringAsync();
        var token = Regex.Match(editPage, @"name=""__RequestVerificationToken""[^>]*value=""([^""]*)""").Groups[1].Value;
        var edit = await client.PostAsync("/Profile/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", token },
            { "DisplayName", "P46 Header" },
            { "Bio", "" },
            { "IconUrl", "https://example.com/p46-header-avatar.png" },
            { "BannerUrl", "" },
        }));
        Assert.True(edit.IsSuccessStatusCode || edit.Headers.Location != null,
            $"profile edit failed: {(int)edit.StatusCode}");

        // Profile header icon is the LCP candidate on the profile page.
        var profile = await (await client.GetAsync($"/Profile?username={username}")).Content.ReadAsStringAsync();
        var imgTags = Regex.Matches(profile, "<img[^>]*>");
        Assert.True(imgTags.Cast<Match>().Any(m => m.Value.Contains("fetchpriority=\"high\"")),
            "Expected the profile header icon to use fetchpriority=\"high\".");
    }
}
