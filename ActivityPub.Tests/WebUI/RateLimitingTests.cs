using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class RateLimitingTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public RateLimitingTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task RegisterAndLogin(HttpClient client, string username)
    {
        await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Rate Limiter" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
    }

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields) => new FormUrlEncodedContent(fields);

    [Fact]
    public async Task ComposePost_IsRateLimited()
    {
        var client = CreateClient();
        var username = $"rl_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterAndLogin(client, username);

        var responses = new List<int>();

        for (var i = 0; i < 25; i++)
        {
            var response = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
            {
                { "Content", $"Post {i}" },
            }));
            responses.Add((int)response.StatusCode);
        }

        Assert.Contains(responses, s => s == 429);
    }

    [Fact]
    public async Task FollowEndpoint_IsRateLimited()
    {
        var client = CreateClient();
        var username = $"rl2_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterAndLogin(client, username);

        var responses = new List<int>();

        for (var i = 0; i < 25; i++)
        {
            var response = await client.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
            {
                { "TargetUser", $"target{i}@example.com" },
            }));
            responses.Add((int)response.StatusCode);
        }

        Assert.Contains(responses, s => s == 429);
    }

    [Fact]
    public async Task NonRateLimitedEndpoint_AllowsManyRequests()
    {
        var client = CreateClient();

        for (var i = 0; i < 30; i++)
        {
            var response = await client.GetAsync("/");
            Assert.True(response.IsSuccessStatusCode, $"Request {i} failed with {(int)response.StatusCode}");
        }
    }
}