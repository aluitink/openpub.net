using ActivityPub.WebUI.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Text.RegularExpressions;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 51.4 — Link previews / OEmbed for outbound URLs (v1). Verifies the
/// server-side OpenGraph/meta + OEmbed parsing, the SSRF-safe URL guard, and the
/// /linkpreview JSON endpoint + client-side hook + CSS.
/// </summary>
public class LinkPreviewTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public LinkPreviewTests(WebUIFactory factory)
    {
        _factory = factory;
    }

    // ---- Pure parsing: OpenGraph / meta -------------------------------------

    [Fact]
    public void ParseOpenGraph_ExtractsOgTitleDescriptionImageSite()
    {
        var html = @"<html><head>
            <title>Fallback title</title>
            <meta property='og:title' content='My Great Article' />
            <meta property='og:description' content='A very interesting read about federated social.' />
            <meta property='og:image' content='https://cdn.example.com/img/cover.jpg' />
            <meta property='og:site_name' content='Example Blog' />
            <meta name='twitter:creator' content='@jane' />
            </head><body></body></html>";

        var p = LinkPreviewService.ParseOpenGraph(html, "https://example.com/post/1");
        Assert.NotNull(p);
        Assert.Equal("My Great Article", p!.Title);
        Assert.Equal("A very interesting read about federated social.", p.Description);
        Assert.Equal("https://cdn.example.com/img/cover.jpg", p.Image);
        Assert.Equal("Example Blog", p.SiteName);
        Assert.Equal("@jane", p.AuthorName);
        Assert.Equal("og", p.Source);
        Assert.Equal("https://example.com/post/1", p.Url);
    }

    [Fact]
    public void ParseOpenGraph_FallsBackToTitleTagAndDescription()
    {
        var html = @"<html><head>
            <title>Only a title here</title>
            <meta name='description' content='A basic page description.' />
            </head></html>";

        var p = LinkPreviewService.ParseOpenGraph(html, "https://example.com/page");
        Assert.NotNull(p);
        Assert.Equal("Only a title here", p!.Title);
        Assert.Equal("A basic page description.", p.Description);
        Assert.Equal("basic", p.Source);
        Assert.Null(p.Image);
    }

    [Fact]
    public void ParseOpenGraph_NoTitle_ReturnsNull()
    {
        var html = @"<html><head><meta name='author' content='nobody'/></head></html>";
        Assert.Null(LinkPreviewService.ParseOpenGraph(html, "https://example.com/x"));
    }

    [Fact]
    public void ParseOpenGraph_HtmlEncodesEntities()
    {
        var html = @"<html><head>
            <meta property='og:title' content='Fish &amp; Chips &lt;Review&gt;' />
            </head></html>";
        var p = LinkPreviewService.ParseOpenGraph(html, "https://example.com/f");
        Assert.NotNull(p);
        Assert.Equal("Fish & Chips <Review>", p!.Title);
    }

    // ---- SSRF-safe URL guard -------------------------------------------------

    [Theory]
    [InlineData("https://example.com/page", true)]
    [InlineData("http://example.com", true)]
    [InlineData("https://user@example.com/", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("mailto:me@example.com", false)]
    [InlineData("/relative/path", false)]
    [InlineData("https://localhost/", false)]
    [InlineData("https://127.0.0.1/secret", false)]
    [InlineData("https://10.0.0.5/admin", false)]
    [InlineData("https://192.168.1.1/rtr", false)]
    [InlineData("https://169.254.169.254/latest/meta-data", false)]
    [InlineData("https://[::1]/", false)]
    [InlineData("ftp://example.com/file", false)]
    [InlineData("   https://example.com/  ", true)]
    public void NormalizeUrl_EnforcesSsrFGuard(string input, bool expectedSafe)
    {
        var result = LinkPreviewService.NormalizeUrl(input);
        if (expectedSafe)
        {
            Assert.NotNull(result);
            Assert.StartsWith("http", result!, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Null(result);
        }
    }

    // ---- Service: caching + null on unsafe ----------------------------------

    [Fact]
    public async Task GetPreviewAsync_UnsafeUrl_ReturnsNullWithoutFetching()
    {
        var svc = new LinkPreviewService(
            new NoopClientFactory(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<LinkPreviewService>.Instance);
        var result = await svc.GetPreviewAsync("https://127.0.0.1/secret");
        Assert.Null(result);
    }

    // ---- Controller endpoint (integration) ----------------------------------

    [Fact]
    public async Task LinkPreviewCard_Endpoint_Returns404ForUnsafeUrl()
    {
        var (client, _) = await GetAuthenticatedUser();
        // An internal URL must be rejected (SSRF guard) → 404, not a fetch.
        var resp = await client.GetAsync("/linkpreview/card?url=https://127.0.0.1/secret");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task LinkPreviewCard_Endpoint_BehavesLikeOtherAuthRoutes()
    {
        // Matches the app's established convention (see RouteAuditTests):
        // anonymous requests to [Authorize] controllers are not a hard 5xx —
        // they're redirected / handled by the status page. The route must simply
        // respond without crashing.
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/linkpreview/card?url=https://example.com/");
        Assert.True(
            resp.StatusCode is System.Net.HttpStatusCode.Redirect or
            System.Net.HttpStatusCode.Found or
            System.Net.HttpStatusCode.Unauthorized or
            System.Net.HttpStatusCode.OK,
            $"Expected redirect/401/200 for unauthenticated link preview, got {(int)resp.StatusCode}");
    }

    // ---- Client JS + CSS -----------------------------------------------------

    [Fact]
    public async Task ClientJs_ContainsLinkPreviewModule()
    {
        var client = _factory.CreateClient();
        var js = await (await client.GetAsync("/js/app.js")).Content.ReadAsStringAsync();
        Assert.Contains("linkpreview", js, StringComparison.Ordinal);
        Assert.Contains("/linkpreview/card?url=", js, StringComparison.Ordinal);
        Assert.Contains("/linkpreview/image?url=", js, StringComparison.Ordinal);
        Assert.Contains("FB.linkPreviewInit", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LinkPreviewCss_DefinesCardStyles()
    {
        var client = _factory.CreateClient();
        var css = await (await client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();
        Assert.Contains(".link-preview {", css, StringComparison.Ordinal);
        Assert.Contains(".link-preview-body {", css, StringComparison.Ordinal);
        Assert.Contains(".link-preview-image {", css, StringComparison.Ordinal);
        Assert.Contains(".link-preview-title {", css, StringComparison.Ordinal);
    }

    // ---- Helpers -------------------------------------------------------------

    async Task<(HttpClient Client, string Username)> GetAuthenticatedUser()
    {
        var client = _factory.CreateClient();
        var username = $"lp_{Guid.NewGuid().ToString("N")[..8]}";
        var register = await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Link Preview" },
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

    /// <summary>
    /// An IHttpClientFactory whose client fails fast, so tests never touch the
    /// network. Used to prove the unsafe-URL path short-circuits before any fetch.
    /// </summary>
    private sealed class NoopClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            throw new InvalidOperationException("No network in unit tests.");
        }
    }
}
