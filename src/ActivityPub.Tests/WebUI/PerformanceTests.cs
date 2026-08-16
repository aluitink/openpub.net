using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class PerformanceTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public PerformanceTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task RegisterUser(HttpClient client, string username, string displayName = "Test User")
    {
        await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", displayName },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
    }

    async Task LoginUser(HttpClient client, string username)
    {
        await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
    }

    static MultipartFormDataContent CreateFormContent(Dictionary<string, string> data)
    {
        var content = new MultipartFormDataContent();
        foreach (var (key, value) in data)
        {
            content.Add(new StringContent(value), $"\"{key}\"");
        }
        return content;
    }

    [Fact]
    public async Task Timeline_Loads_Under_3_Seconds()
    {
        var client = CreateClient();
        var username = $"perf_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);

        for (int i = 0; i < 10; i++)
        {
            await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
            {
                { "Content", $"Performance test post {i}" },
            }));
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.GetAsync("/timeline");
        sw.Stop();

        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        Assert.True(sw.ElapsedMilliseconds < 3000, $"Timeline took {sw.ElapsedMilliseconds}ms, expected < 3000ms");
    }

    [Fact]
    public async Task Profile_Loads_Under_2_Seconds()
    {
        var client = CreateClient();
        var username = $"perf2_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);

        for (int i = 0; i < 5; i++)
        {
            await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
            {
                { "Content", $"Profile test post {i}" },
            }));
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.GetAsync($"/profile?username={username}");
        sw.Stop();

        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Profile took {sw.ElapsedMilliseconds}ms, expected < 2000ms");
    }

    [Fact]
    public async Task HashtagSearch_Loads_Under_2_Seconds()
    {
        var client = CreateClient();
        var username = $"perf3_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);

        var tag = $"perftag{Guid.NewGuid().ToString("N")[..6]}";
        for (int i = 0; i < 10; i++)
        {
            await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
            {
                { "Content", $"Hashtag post #{tag}" },
            }));
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.GetAsync($"/hashtag/{tag}");
        sw.Stop();

        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Hashtag search took {sw.ElapsedMilliseconds}ms, expected < 2000ms");
    }

    [Fact]
    public async Task ActorsApi_Outbox_Returns_OrderedCollectionPage()
    {
        var client = CreateClient();
        var username = $"perf4_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Outbox page test" },
        }));

        var response = await client.GetAsync($"/actors/outbox/{username}");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("OrderedCollectionPage", body);
        Assert.Contains("first", body);
        Assert.Contains("last", body);
        Assert.Contains("partOf", body);
    }

    [Fact]
    public async Task MultipleFollows_Dont_Degrade_Performance()
    {
        var client = CreateClient();
        var target = $"perf5_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, target, "Target User");
        await LoginUser(client, target);

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Target post" },
        }));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 5; i++)
        {
            var follower = $"perf5f{i}_{Guid.NewGuid().ToString("N")[..6]}";
            var fclient = _factory.CreateClient();
            await RegisterUser(fclient, follower, $"Follower {i}");
            await LoginUser(fclient, follower);

            await fclient.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
            {
                { "Username", target },
            }));
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 10000, $"5 follows took {sw.ElapsedMilliseconds}ms, expected < 10000ms");
    }
}
