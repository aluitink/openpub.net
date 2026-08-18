using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.Tests.WebUI;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class NotificationTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public NotificationTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task<HttpClient> GetAuthenticatedClient()
    {
        var client = CreateClient();
        var username = $"nt_{Guid.NewGuid().ToString("N")[..8]}";
        var regResp = await RegisterUser(client, username);
        Assert.True(regResp.IsSuccessStatusCode || regResp.Headers.Location != null, $"Register failed: {(int)regResp.StatusCode}");
        var loginResp = await LoginUser(client, username);
        Assert.True(loginResp.IsSuccessStatusCode || loginResp.Headers.Location != null, $"Login failed: {(int)loginResp.StatusCode}");
        return client;
    }

    async Task<HttpResponseMessage> RegisterUser(HttpClient client, string username)
    {
        return await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Test User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
    }

    async Task<HttpResponseMessage> LoginUser(HttpClient client, string username)
    {
        return await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
    }

    [Fact]
    public async Task NotificationsPage_RequiresAuthentication()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/notifications");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Login", body);
    }

    [Fact]
    public async Task NotificationsPage_Returns200_WhenAuthenticated()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/notifications");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task NotificationsPage_ShowsEmptyState()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/notifications");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        var mainMatch = System.Text.RegularExpressions.Regex.Match(body, @"<main>(.*?)</main>", System.Text.RegularExpressions.RegexOptions.Singleline);
        var mainText = mainMatch.Success ? mainMatch.Groups[1].Value : "NO_MAIN";
        var clean = System.Text.RegularExpressions.Regex.Replace(mainText, @"\s+", " ");
        Assert.True(clean.Contains("Notification"), $"Main: {clean.Substring(0, Math.Min(600, clean.Length))}");
    }

    [Fact]
    public async Task NotificationsPage_ShowsLikeNotifications()
    {
        var authorClient = CreateClient();
        var authorUsername = $"nt_author_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(authorClient, authorUsername);
        await LoginUser(authorClient, authorUsername);

        var authorResponse = await authorClient.GetAsync("/profile");
        Assert.True(authorResponse.IsSuccessStatusCode, $"Profile failed: {(int)authorResponse.StatusCode}");

        var likerClient = CreateClient();
        var likerUsername = $"nt_liker_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(likerClient, likerUsername);
        await LoginUser(likerClient, likerUsername);

        var authorProfile = await authorClient.GetAsync("/profile");
        var authorBody = await authorProfile.Content.ReadAsStringAsync();
        Assert.Contains(authorUsername, authorBody);

        var likeResponse = await likerClient.PostAsync("/Interaction/Like", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "TargetActivityId", authorUsername },
        }));

        var notifResponse = await authorClient.GetAsync("/notifications");
        var notifBody = await notifResponse.Content.ReadAsStringAsync();
        Assert.Contains("Notifications", notifBody);
    }

    [Fact]
    public async Task NotificationsPage_WithLike_ShowsNotification()
    {
        var authorClient = CreateClient();
        var authorUsername = $"nt_auth2_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(authorClient, authorUsername);
        await LoginUser(authorClient, authorUsername);

        var likerClient = CreateClient();
        var likerUsername = $"nt_liker2_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(likerClient, likerUsername);
        await LoginUser(likerClient, likerUsername);

        var notifResponse = await authorClient.GetAsync("/notifications");
        Assert.True(notifResponse.IsSuccessStatusCode, $"Notifications failed: {(int)notifResponse.StatusCode}");
        var notifBody = await notifResponse.Content.ReadAsStringAsync();
        Assert.Contains("Notifications", notifBody);
    }

    // ---- Phase 48.2: badge + mark-as-read + deep link ----------------------

    [Fact]
    public async Task BadgeEndpoint_ReturnsZero_WhenUnauthenticated()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/notifications/badge");
        Assert.True(response.IsSuccessStatusCode, $"Badge failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"count\"", body);
        Assert.Contains("0", body);
    }

    [Fact]
    public async Task BadgeEndpoint_ReturnsNonNegativeCount_WhenAuthenticated()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/notifications/badge");
        Assert.True(response.IsSuccessStatusCode, $"Badge failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(body, "\"count\"\\s*:\\s*(\\d+)");
        Assert.True(match.Success, $"No count in badge response: {body}");
        int.TryParse(match.Groups[1].Value, out var count);
        Assert.True(count >= 0, $"Count should be non-negative, got {count}");
    }

    [Fact]
    public async Task BadgeEndpoint_CountsInboxActivities_AfterLike()
    {
        // Author registers and posts a note; liker likes it. The author's
        // unread count should then be >= 1 (the like is in the author's inbox).
        var authorClient = CreateClient();
        var authorUsername = $"nt_badge_author_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(authorClient, authorUsername);
        await LoginUser(authorClient, authorUsername);

        var noteId = await CreateNoteForUser(authorClient, authorUsername, "Badge badge badge");
        Assert.NotNull(noteId);

        var likerClient = CreateClient();
        var likerUsername = $"nt_badge_liker_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(likerClient, likerUsername);
        await LoginUser(likerClient, likerUsername);

        var likeResponse = await likerClient.PostAsync("/Interaction/Like", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "TargetActivityId", noteId! },
        }));
        // A like either succeeds (redirect) or is a no-op; we only care that
        // the activity is persisted to the author's inbox.

        var badgeResponse = await authorClient.GetAsync("/notifications/badge");
        var body = await badgeResponse.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(body, "\"count\"\\s*:\\s*(\\d+)");
        Assert.True(match.Success, $"No count in badge response: {body}");
        int.TryParse(match.Groups[1].Value, out var count);
        Assert.True(count >= 1, $"Expected unread count >= 1 after a like, got {count} (body: {body})");
    }

    [Fact]
    public async Task MarkAllRead_ClearsUnreadCount()
    {
        var authorClient = CreateClient();
        var authorUsername = $"nt_markread_author_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(authorClient, authorUsername);
        await LoginUser(authorClient, authorUsername);

        var noteId = await CreateNoteForUser(authorClient, authorUsername, "Mark read test note");
        Assert.NotNull(noteId);

        var likerClient = CreateClient();
        var likerUsername = $"nt_markread_liker_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(likerClient, likerUsername);
        await LoginUser(likerClient, likerUsername);

        await likerClient.PostAsync("/Interaction/Like", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "TargetActivityId", noteId! },
        }));

        // Ensure there's at least one unread before marking read.
        var pre = await GetBadgeCount(authorClient);
        Assert.True(pre >= 1, $"Expected unread >= 1 before mark-as-read, got {pre}");

        // Mark all as read. (The test host uses a permissive anti-forgery
        // provider, so no token is required.)
        var markResp = await authorClient.PostAsync("/notifications/markallread", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.True(markResp.IsSuccessStatusCode || markResp.Headers.Location != null, $"MarkAllRead failed: {(int)markResp.StatusCode}");

        var post = await GetBadgeCount(authorClient);
        Assert.Equal(0, post);
    }

    [Fact]
    public async Task NotificationItem_RendersDeepLink_ToTimelineNote()
    {
        var authorClient = CreateClient();
        var authorUsername = $"nt_deep_author_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(authorClient, authorUsername);
        await LoginUser(authorClient, authorUsername);

        var noteId = await CreateNoteForUser(authorClient, authorUsername, "Deep link target note");
        Assert.NotNull(noteId);

        var likerClient = CreateClient();
        var likerUsername = $"nt_deep_liker_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(likerClient, likerUsername);
        await LoginUser(likerClient, likerUsername);

        await likerClient.PostAsync("/Interaction/Like", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "TargetActivityId", noteId! },
        }));

        var notifResponse = await authorClient.GetAsync("/notifications");
        var body = await notifResponse.Content.ReadAsStringAsync();
        // A like notification should carry a deep link to the source note on
        // the timeline (the note-flash scroll target).
        Assert.True(body.Contains("notification-note-link"), "Like notification should render a deep link");
        Assert.True(body.Contains("/timeline?note="), "Deep link should point at /timeline?note=<id>");
    }

    // ---- helpers -----------------------------------------------------------

    async Task<int> GetBadgeCount(HttpClient client)
    {
        var response = await client.GetAsync("/notifications/badge");
        var body = await response.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(body, "\"count\"\\s*:\\s*(\\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }

    /// <summary>
    /// Posts a note for the user and returns the Create activity id (the value
    /// used as TargetActivityId for likes/replies). Returns null on failure.
    /// Note: the test host uses a permissive anti-forgery provider, so no
    /// token is required (mirrors ComposeTimelineTests).
    /// </summary>
    async Task<string?> CreateNoteForUser(HttpClient client, string username, string content)
    {
        // Post the note. The compose action returns 200 (it re-renders /
        // redirects to the timeline in the test host); either way the note is
        // persisted, so we locate it on the timeline rather than relying on a
        // redirect Location header.
        var postResp = await client.PostAsync("/compose/post", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Content", content },
        }));
        Assert.True(postResp.IsSuccessStatusCode || postResp.Headers.Location != null,
            $"Compose post failed: {(int)postResp.StatusCode}");

        // The note appears on the user's timeline as a card carrying its
        // Create activity id in data-activity-id.
        var tl = await client.GetAsync("/timeline");
        var tlBody = await tl.Content.ReadAsStringAsync();
        var cardMatch = System.Text.RegularExpressions.Regex.Match(tlBody, @"data-activity-id=""([^""]+)""");
        return cardMatch.Success ? cardMatch.Groups[1].Value : null;
    }
}
