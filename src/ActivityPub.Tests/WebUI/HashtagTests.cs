using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class HashtagTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public HashtagTests(WebUIFactory factory)
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

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields) => new FormUrlEncodedContent(fields);

    [Fact]
    public async Task HashtagPage_Returns200_ForValidTag()
    {
        var client = CreateClient();
        var username = $"ht_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Testing hashtag support #fediblog" },
        }));

        var response = await client.GetAsync("/hashtag/fediblog");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task HashtagPage_FindsNotesByHashtag()
    {
        var client = CreateClient();
        var username = $"ht2_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Hello world #hashtest123 unique marker" },
        }));

        var response = await client.GetAsync("/hashtag/hashtest123");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("hashtest123", body);
    }

    [Fact]
    public async Task HashtagPage_WithHashPrefix_ReturnsSameResults()
    {
        var client = CreateClient();
        var username = $"ht3_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Another post #hashtagtest456" },
        }));

        var response = await client.GetAsync("/hashtag/%23hashtagtest456");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("hashtagtest456", body);
    }

    [Fact]
    public async Task HashtagPage_EmptyTag_Returns404()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/hashtag/");
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task HashtagPage_NoNotes_ShowsEmptyState()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/hashtag/nonexistent123");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("No notes found", body);
    }
}