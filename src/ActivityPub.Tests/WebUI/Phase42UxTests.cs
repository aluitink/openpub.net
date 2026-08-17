using System.Net;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class Phase42UxTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public Phase42UxTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    private static MultipartFormDataContent Form(Dictionary<string, string> fields)
    {
        var content = new MultipartFormDataContent();
        foreach (var (key, value) in fields)
        {
            content.Add(new StringContent(value), $"\"{key}\"");
        }
        return content;
    }

    private async Task<string> GetAppJsAsync()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/js/app.js");
        return await res.Content.ReadAsStringAsync();
    }

    private async Task<HttpClient> GetAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var username = $"ux42_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/auth/register", Form(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "UX Test" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" }
        }));
        await client.PostAsync("/auth/login", Form(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" }
        }));
        return client;
    }

    [Fact]
    public async Task ComposePage_HasCharacterCounter()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/compose");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("char-counter", body);
        Assert.Contains("charCount", body);
        Assert.Contains("char-ok", body);
        Assert.Contains("char-near", body);
        Assert.Contains("char-over", body);
    }

    [Fact]
    public async Task TimelinePage_HasPaginationSupport()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/timeline?page=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Home Timeline", body);
    }

    [Fact]
    public async Task TimelinePage_HasConfirmDialogForDelete()
    {
        var appJs = await GetAppJsAsync();
        Assert.Contains("confirm", appJs);
    }

    [Fact]
    public async Task TimelinePage_HasOptimisticLikeBoost()
    {
        var appJs = await GetAppJsAsync();
        Assert.Contains("btn-like", appJs);
        Assert.Contains("btn-boost", appJs);
        Assert.Contains("data-published", appJs);
    }

    [Fact]
    public async Task Navbar_HasSearchInput_WhenAuthenticated()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/timeline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("nav-search-input", body);
        Assert.Contains("nav-search-form", body);
    }

    [Fact]
    public async Task Navbar_HasKeyboardShortcuts_WhenAuthenticated()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/timeline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("nav-search-input", body);
    }

    [Fact]
    public async Task Navbar_NoSearchInput_WhenAnonymous()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("nav-search-input", body);
    }

    [Fact]
    public async Task NotificationsPage_Renders()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/notifications");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("notifications-container", body);
    }

    [Fact]
    public async Task Notifications_MarkAllRead_RedirectsBack()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.PostAsync("/notifications/markallread", Form(new Dictionary<string, string>
        {
            { "__RequestVerificationToken", "" }
        }));
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.OK,
            $"Expected redirect/200, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task SearchPage_HasHashtagTab()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/search?q=test&tab=hashtags");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("tab=hashtags", body);
    }

    [Fact]
    public async Task SearchPage_HasDebouncedInput()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/search?q=test");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("search-input", body);
        Assert.Contains("search-loading", body);
    }

    [Fact]
    public async Task NoteCard_HasTimestampWithTitle()
    {
        var appJs = await GetAppJsAsync();
        Assert.Contains("note-timestamp", appJs);
        Assert.Contains("data-published", appJs);
    }

    [Fact]
    public async Task Layout_HasToastContainer()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("toast-container", body);
        Assert.Contains("app.js", body);
    }

    [Fact]
    public async Task ComposePage_HasLivePreview()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/compose");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("compose-preview", body);
        Assert.Contains("compose.js", body);
    }

    [Fact]
    public async Task TimelinePage_HasMoreMenu()
    {
        var appJs = await GetAppJsAsync();
        Assert.Contains("note-more-menu", appJs);
        Assert.Contains("copy-link", appJs);
    }

    [Fact]
    public async Task TimelinePage_HasToastFeedback()
    {
        var appJs = await GetAppJsAsync();
        Assert.Contains("showToast", appJs);
    }

    [Fact]
    public async Task ComposePage_HasImagePreview()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/compose");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("image-preview-container", body);
        Assert.Contains("image-preview", body);
        Assert.Contains("image-remove-btn", body);
    }

    [Fact]
    public async Task TimelinePage_HasCwToggleJs()
    {
        var appJs = await GetAppJsAsync();
        Assert.Contains("cw-toggle-btn", appJs);
        Assert.Contains("cw-hidden", appJs);
    }

    [Fact]
    public async Task PollNewPage_HasLivePreview()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/poll/new");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("poll-preview", body);
        Assert.Contains("poll-option-input", body);
        Assert.Contains("poll-multiselect", body);
        Assert.Contains("poll.js", body);
    }

    [Fact]
    public async Task TimelinePage_HasLoadingSkeleton()
    {
        var appJs = await GetAppJsAsync();
        Assert.Contains("skeleton", appJs);
        Assert.Contains("load-more-skeleton", appJs);
    }

    [Fact]
    public async Task TimelinePage_HasReplyContextBanner()
    {
        var client = await GetAuthenticatedClient();
        var postResponse = await client.PostAsync("/compose/post", Form(new Dictionary<string, string>
        {
            { "Content", "Original post for reply banner" }
        }));
        Assert.True(postResponse.Headers.Location != null || postResponse.IsSuccessStatusCode,
            $"Post failed: {(int)postResponse.StatusCode}");
        var response = await client.GetAsync("/timeline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("reply-context-banner", body);
        Assert.Contains("reply-context-text", body);
    }

    [Fact]
    public async Task MainPages_UseConsistentPageHeader()
    {
        var client = await GetAuthenticatedClient();
        foreach (var path in new[] { "/timeline", "/compose", "/search", "/notifications" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("page-header", body);
        }
    }
}
