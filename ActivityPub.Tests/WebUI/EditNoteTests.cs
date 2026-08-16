using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class EditNoteTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public EditNoteTests(WebUIFactory factory)
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
            { "DisplayName", "Edit User" },
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
    public async Task EditNote_SucceedsForOwnedNote()
    {
        var (client, _) = await RegisterAndLogin($"edit_own_{Guid.NewGuid().ToString("N")[..8]}");

        var createResp = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Original note content" },
        }));
        Assert.True(createResp.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var activity = await db.Activities
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(activity);

        var editResp = await client.PostAsync("/compose/editnote", CreateFormContent(new Dictionary<string, string>
        {
            { "ActivityId", activity.ActivityId },
            { "Content", "Updated note content" },
        }));
        Assert.True(editResp.IsSuccessStatusCode, $"Failed: {(int)editResp.StatusCode}");

        var updateActivity = await db.Activities
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(updateActivity);
        Assert.Contains("\"type\":\"Update\"", updateActivity.JsonData);
    }

    [Fact]
    public async Task EditNote_RejectsEmptyContent()
    {
        var (client, _) = await RegisterAndLogin($"edit_empty_{Guid.NewGuid().ToString("N")[..8]}");

        var createResp = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Original note content" },
        }));
        Assert.True(createResp.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var activity = await db.Activities
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(activity);

        var editResp = await client.PostAsync("/compose/editnote", CreateFormContent(new Dictionary<string, string>
        {
            { "ActivityId", activity.ActivityId },
            { "Content", "" },
        }));
        Assert.True(editResp.IsSuccessStatusCode);
    }

    [Fact]
    public async Task EditNote_RetransmitsToTimeline()
    {
        var (client, _) = await RegisterAndLogin($"edit_retransmit_{Guid.NewGuid().ToString("N")[..8]}");

        var createResp = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Original note content" },
        }));
        Assert.True(createResp.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var activity = await db.Activities
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(activity);

        var editResp = await client.PostAsync("/compose/editnote", CreateFormContent(new Dictionary<string, string>
        {
            { "ActivityId", activity.ActivityId },
            { "Content", "Updated note content" },
        }));

        Assert.True(editResp.IsSuccessStatusCode, $"Failed: {(int)editResp.StatusCode}");

        var activities = await db.Activities.ToListAsync();
        var updateActivities = activities.Where(a => a.JsonData.Contains("\"type\":\"Update\"")).ToList();
        Assert.NotEmpty(updateActivities);
        Assert.Contains(updateActivities, a => a.JsonData.Contains("Updated note content"));
    }
}
