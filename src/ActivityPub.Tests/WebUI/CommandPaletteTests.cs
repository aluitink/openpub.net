using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class CommandPaletteTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public CommandPaletteTests(WebUIFactory factory)
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

    async Task<string> CreateTestUserAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var activityDb = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var user = new ApplicationUser
        {
            UserName = username,
            Email = $"{username}@test.com",
            DisplayName = username,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        identityDb.Users.Add(user);
        await identityDb.SaveChangesAsync();
        var actorId = $"https://localhost/users/{username}";
        user.ActorId = actorId;
        await identityDb.SaveChangesAsync();

        var actor = new Actor
        {
            Id = actorId,
            Name = username,
            PreferredUsername = username,
            Summary = $"Test user {username}",
            Inbox = $"{actorId}/inbox",
            Outbox = $"{actorId}/outbox",
            Followers = $"{actorId}/followers",
            Following = $"{actorId}/following",
            Type = "Person",
            PublicKey = new PublicKey
            {
                Id = $"{actorId}/keys/main",
                Owner = actorId,
                PublicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0Z3VS5JJcds3xfn/ygWe\n-----END PUBLIC KEY-----"
            }
        };

        activityDb.Actors.Add(new ActorEntity
        {
            Username = username,
            JsonData = JsonSerializer.Serialize(actor, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            })
        });
        await activityDb.SaveChangesAsync();
        return actorId;
    }

    async Task<JsonElement> SearchJsonAsync(HttpClient client, string q)
    {
        var response = await client.GetAsync("/search/json?q=" + Uri.EscapeDataString(q));
        Assert.True(response.IsSuccessStatusCode, $"search/json failed: {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task SearchJson_EmptyQuery_ReturnsEmptyArrays()
    {
        var client = CreateClient();
        await RegisterUser(client, $"p_empty_{Guid.NewGuid():N}".Substring(0, 20));
        await LoginUser(client, $"p_empty_{Guid.NewGuid():N}".Substring(0, 20));

        var response = await client.GetAsync("/search/json");
        Assert.True(response.IsSuccessStatusCode, $"search/json failed: {(int)response.StatusCode}");
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("notes").ValueKind);
        Assert.Equal(0, doc.RootElement.GetProperty("notes").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("users").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("hashtags").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("communities").GetArrayLength());
    }

    [Fact]
    public async Task SearchJson_FindsUserByUsername()
    {
        var client = CreateClient();
        var username = $"paluser_{Guid.NewGuid():N}".Substring(0, 24);
        await RegisterUser(client, username, username);

        var root = await SearchJsonAsync(client, username);
        var users = root.GetProperty("users");
        var found = users.EnumerateArray().Any(u =>
            string.Equals(u.GetProperty("username").GetString(), username, StringComparison.OrdinalIgnoreCase));
        Assert.True(found, $"Expected user {username} in palette users, got: {users.GetRawText()}");
    }

    [Fact]
    public async Task SearchJson_FindsNoteByContent()
    {
        var client = CreateClient();
        var username = $"palnote_{Guid.NewGuid():N}".Substring(0, 24);
        await RegisterUser(client, username);
        await LoginUser(client, username);

        var marker = "palettemarker" + Guid.NewGuid().ToString("N")[..12];
        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", $"Hello {marker} from the command palette" },
        }));

        var root = await SearchJsonAsync(client, marker);
        var notes = root.GetProperty("notes");
        var found = notes.EnumerateArray().Any(n =>
            (n.GetProperty("content").GetString() ?? "").Contains(marker));
        Assert.True(found, $"Expected note containing {marker} in palette notes, got: {notes.GetRawText()}");
    }

    [Fact]
    public async Task SearchJson_FindsCommunityByName()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();
        var ownerId = await CreateTestUserAsync($"palcomowner_{Guid.NewGuid():N}".Substring(0, 32));
        var name = "Zephyr" + Guid.NewGuid().ToString("N")[..10];
        await communityService.CreateCommunityAsync(ownerId, name, "a palette test community");

        var client = CreateClient();
        var root = await SearchJsonAsync(client, name);
        var communities = root.GetProperty("communities");
        var found = communities.EnumerateArray().Any(c =>
            string.Equals(c.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase));
        Assert.True(found, $"Expected community {name} in palette results, got: {communities.GetRawText()}");
    }

    [Fact]
    public async Task SearchJson_NoMatches_ReturnsEmptyArrays()
    {
        var client = CreateClient();
        await RegisterUser(client, $"palnomatch_{Guid.NewGuid():N}".Substring(0, 24));
        await LoginUser(client, $"palnomatch_{Guid.NewGuid():N}".Substring(0, 24));

        var root = await SearchJsonAsync(client, "zzznonexistentzzz" + Guid.NewGuid().ToString("N")[..6]);
        Assert.Equal(0, root.GetProperty("notes").GetArrayLength());
        Assert.Equal(0, root.GetProperty("users").GetArrayLength());
        Assert.Equal(0, root.GetProperty("hashtags").GetArrayLength());
        Assert.Equal(0, root.GetProperty("communities").GetArrayLength());
    }

    [Fact]
    public async Task AuthenticatedPage_ContainsPaletteMarkup()
    {
        var client = CreateClient();
        var username = $"palpage_{Guid.NewGuid():N}".Substring(0, 24);
        await RegisterUser(client, username);
        await LoginUser(client, username);

        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.Contains("palette-overlay", body);
        Assert.Contains("palette-dialog", body);
        Assert.Contains("palette-input", body);
        Assert.Contains("palette-results", body);
        Assert.Contains("js/palette.js", body);
    }

    [Fact]
    public async Task UnauthenticatedPage_DoesNotContainPalette()
    {
        var client = CreateClient();
        var body = await (await client.GetAsync("/timeline")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("palette-overlay", body);
    }
}
