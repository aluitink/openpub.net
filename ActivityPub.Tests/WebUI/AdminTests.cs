using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class AdminTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public AdminTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task<(HttpClient Client, string Username)> RegisterAndLogin(string username, string displayName = "Test User")
    {
        var client = CreateClient();
        await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", displayName },
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

    async Task MakeAdmin(ApplicationDbContext db, string username)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (user != null)
        {
            user.IsAdmin = true;
            await db.SaveChangesAsync();
        }
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
    public async Task AdminDashboard_DeniesNonAdmin()
    {
        var (client, _) = await RegisterAndLogin($"adm_denied_{Guid.NewGuid().ToString("N")[..8]}");

        var response = await client.GetAsync("/admin/dashboard");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Admin Dashboard", body);
    }

    [Fact]
    public async Task AdminDashboard_AllowsAdmin()
    {
        var (client, username) = await RegisterAndLogin($"adm_{Guid.NewGuid().ToString("N")[..8]}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await MakeAdmin(db, username);

        var response = await client.GetAsync("/admin/dashboard");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Admin Dashboard", body);
    }

    [Fact]
    public async Task AdminUsers_DeniesNonAdmin()
    {
        var (client, _) = await RegisterAndLogin($"adm_users_denied_{Guid.NewGuid().ToString("N")[..8]}");

        var response = await client.GetAsync("/admin/users");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Manage Users", body);
    }

    [Fact]
    public async Task AdminUsers_ListsAllUsers()
    {
        var (client, adminUsername) = await RegisterAndLogin($"adm_users_{Guid.NewGuid().ToString("N")[..8]}", "Admin User");

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Admin test post" },
        }));

        var regularUser = $"adm_regular_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterAndLogin(regularUser, "Regular User");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await MakeAdmin(db, adminUsername);

        var response = await client.GetAsync("/admin/users");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(adminUsername, body);
        Assert.Contains(regularUser, body);
    }

    [Fact]
    public async Task AdminModeration_DeniesNonAdmin()
    {
        var (client, _) = await RegisterAndLogin($"adm_mod_denied_{Guid.NewGuid().ToString("N")[..8]}");

        var response = await client.GetAsync("/admin/moderation");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Recent Activities", body);
    }

    [Fact]
    public async Task AdminModeration_ShowsActivities()
    {
        var (client, username) = await RegisterAndLogin($"adm_mod_{Guid.NewGuid().ToString("N")[..8]}");

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Moderation test post" },
        }));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await MakeAdmin(db, username);

        var response = await client.GetAsync("/admin/moderation");
        Assert.True(response.IsSuccessStatusCode, $"Failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Moderation", body);
    }
}
