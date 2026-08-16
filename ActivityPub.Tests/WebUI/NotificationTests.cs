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

        var likeResponse = await likerClient.PostAsync("/interactions/like", new FormUrlEncodedContent(new Dictionary<string, string>
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
}
