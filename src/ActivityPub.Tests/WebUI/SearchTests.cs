using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class SearchTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public SearchTests(WebUIFactory factory)
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
    public async Task SearchPage_Returns200()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/search");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task SearchPage_ShowsSearchForm()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/search");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Search", body);
        Assert.Contains("search-input", body);
    }

    [Fact]
    public async Task SearchNotes_ReturnsMatchingNotes()
    {
        var client = CreateClient();
        var username = $"st_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Hello world unique keyword searchtest123" },
        }));

        var response = await client.GetAsync("/search?q=unique+keyword&tab=notes");
        Assert.True(response.IsSuccessStatusCode, $"Search failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("unique keyword", body);
    }

    [Fact]
    public async Task SearchUsers_FindsByUsername()
    {
        var username = $"stuser_{Guid.NewGuid().ToString("N")[..8]}";
        var client = CreateClient();
        await RegisterUser(client, username, username.Replace("stuser_", ""));

        var response = await client.GetAsync($"/search?q={username}&tab=users");
        Assert.True(response.IsSuccessStatusCode, $"Search failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(username, body);
    }

    [Fact]
    public async Task SearchUsers_FindsByDisplayName()
    {
        var client = CreateClient();
        var displayName = "UniqueDisplayName123";
        var username = $"stdisp_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username, displayName);

        var response = await client.GetAsync($"/search?q={displayName}&tab=users");
        Assert.True(response.IsSuccessStatusCode, $"Search failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(displayName, body);
    }

    [Fact]
    public async Task SearchNotes_NoResults_ShowsEmptyMessage()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/search?q=xyznonexistent123&tab=notes");
        Assert.True(response.IsSuccessStatusCode, $"Search failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("No notes found", body);
    }

    [Fact]
    public async Task SearchUsers_NoResults_ShowsEmptyMessage()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/search?q=xyznonexistent456&tab=users");
        Assert.True(response.IsSuccessStatusCode, $"Search failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("No users found", body);
    }
}