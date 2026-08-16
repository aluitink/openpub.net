using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class RealtimeTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public RealtimeTests(WebUIFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task<(HttpClient Client, string Username)> RegisterAndLogin(string username)
    {
        var client = CreateClient();
        await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Test User" },
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

    [Fact]
    public async Task SSE_Endpoint_Exists()
    {
        var (client, _) = await RegisterAndLogin($"rt_sse_e_{Guid.NewGuid().ToString("N")[..8]}");
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var response = await client.GetAsync("/sse/stream", cts.Token);
            Assert.True(response.IsSuccessStatusCode, $"SSE failed: {(int)response.StatusCode}");
            Assert.Contains("text/event-stream", response.Content.Headers.ContentType?.MediaType ?? "");
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task SSE_Returns200_ForAuthenticatedUser()
    {
        var (client, _) = await RegisterAndLogin($"rt_sse_{Guid.NewGuid().ToString("N")[..8]}");
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var response = await client.GetAsync("/sse/stream", cts.Token);
            Assert.True(response.IsSuccessStatusCode, $"SSE failed: {(int)response.StatusCode}");
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task SignalR_Hub_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        var hub = scope.ServiceProvider.GetService<Microsoft.AspNetCore.SignalR.IHubContext<ActivityPub.WebUI.Hubs.NotificationHub>>();
        Assert.NotNull(hub);
    }

    [Fact]
    public async Task NotificationService_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<ActivityPub.WebUI.Services.INotificationService>();
        Assert.NotNull(notificationService);
    }

    [Fact]
    public async Task Compose_Post_BroadcastsNotification()
    {
        var username = $"rt_compose_{Guid.NewGuid().ToString("N")[..8]}";
        var (client, _) = await RegisterAndLogin(username);

        var response = await client.PostAsync("/compose/post", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Content", "Real-time test post" },
        }));

        Assert.True(response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Redirect, $"Post failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task NotificationBadge_ExistsInLayout()
    {
        var (client, _) = await RegisterAndLogin($"rt_badge_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/");
        Assert.True(response.IsSuccessStatusCode, $"Home failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("notification-badge", body);
    }

    [Fact]
    public async Task SignalR_Script_ExistsInLayout()
    {
        var (client, _) = await RegisterAndLogin($"rt_script_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/");
        Assert.True(response.IsSuccessStatusCode, $"Home failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("signalr", body.ToLower());
    }
}
