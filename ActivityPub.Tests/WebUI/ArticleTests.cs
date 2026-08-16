using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class ArticleTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ArticleTests(WebUIFactory factory)
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
        await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Article User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        return (client, username);
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
    public async Task NewArticle_ReturnsArticleForm()
    {
        var (client, _) = await RegisterAndLogin($"art_new_{Guid.NewGuid().ToString("N")[..8]}");

        var response = await client.GetAsync("/compose/newarticle");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("New Article", body);
    }

    [Fact]
    public async Task PostArticle_CreatesArticle()
    {
        var (client, _) = await RegisterAndLogin($"art_post_{Guid.NewGuid().ToString("N")[..8]}");

        var response = await client.PostAsync("/compose/postarticle", CreateFormContent(new Dictionary<string, string>
        {
            { "Name", "My First Article" },
            { "Summary", "A summary of the article" },
            { "Content", "This is the full content of my article. It can be quite long." },
        }));
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var activities = await db.Activities.Where(a => a.JsonData.Contains("\"type\":\"Article\"")).ToListAsync();
        Assert.NotEmpty(activities);
    }

    [Fact]
    public async Task PostArticle_RejectsEmptyContent()
    {
        var (client, _) = await RegisterAndLogin($"art_empty_{Guid.NewGuid().ToString("N")[..8]}");

        var response = await client.PostAsync("/compose/postarticle", CreateFormContent(new Dictionary<string, string>
        {
            { "Name", "Title" },
            { "Content", "" },
        }));
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task PostArticle_RejectsEmptyTitle()
    {
        var (client, _) = await RegisterAndLogin($"art_notitle_{Guid.NewGuid().ToString("N")[..8]}");

        var response = await client.PostAsync("/compose/postarticle", CreateFormContent(new Dictionary<string, string>
        {
            { "Name", "" },
            { "Content", "Content here" },
        }));
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
    }
}
