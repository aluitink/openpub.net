using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Repositories;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class DiscoveryTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public DiscoveryTests(WebUIFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    private async Task<string> CreateTestUserAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var activityDb = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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

    [Fact]
    public async Task GetTrendingHashtagsAsync_Returns_Hashtags_From_Activities()
    {
        using var scope = _factory.Services.CreateScope();
        var activityDb = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var json1 = @"{""id"":""https://localhost/activity/1"",""type"":""Create"",""object"":{""id"":""https://localhost/note/1"",""type"":""Note"",""content"":""Hello #dotnet #activitypub world"",""tag"":[""#dotnet"",""#activitypub""]}}";
        var json2 = @"{""id"":""https://localhost/activity/2"",""type"":""Create"",""object"":{""id"":""https://localhost/note/2"",""type"":""Note"",""content"":""Testing #dotnet again"",""tag"":[""#dotnet""]}}";

        activityDb.Activities.Add(new ActivityEntity
        {
            ActivityId = "https://localhost/activity/1",
            JsonData = json1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        activityDb.Activities.Add(new ActivityEntity
        {
            ActivityId = "https://localhost/activity/2",
            JsonData = json2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await activityDb.SaveChangesAsync();

        var trending = await discovery.GetTrendingHashtagsAsync();

        Assert.NotEmpty(trending);
        var dotnetTag = trending.FirstOrDefault(t => t.Tag == "#dotnet");
        Assert.NotNull(dotnetTag);
        Assert.True(dotnetTag.Count >= 2);
    }

    [Fact]
    public async Task GetTrendingHashtagsAsync_With_TimeWindow_Filters_Results()
    {
        using var scope = _factory.Services.CreateScope();
        var activityDb = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var oldJson = @"{""id"":""https://localhost/activity/old"",""type"":""Create"",""object"":{""id"":""https://localhost/note/old"",""type"":""Note"",""content"":""#old hashtag"",""tag"":[""#old""]}}";
        var newJson = @"{""id"":""https://localhost/activity/new"",""type"":""Create"",""object"":{""id"":""https://localhost/note/new"",""type"":""Note"",""content"":""#recent hashtag"",""tag"":[""#recent""]}}";

        activityDb.Activities.Add(new ActivityEntity
        {
            ActivityId = "https://localhost/activity/old",
            JsonData = oldJson,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });

        activityDb.Activities.Add(new ActivityEntity
        {
            ActivityId = "https://localhost/activity/new",
            JsonData = newJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await activityDb.SaveChangesAsync();

        var trending = await discovery.GetTrendingHashtagsAsync(TimeSpan.FromHours(1));

        Assert.DoesNotContain(trending, t => t.Tag == "#old");
        Assert.Contains(trending, t => t.Tag == "#recent");
    }

    [Fact]
    public async Task GetFollowerSuggestionsAsync_Returns_Empty_For_New_User()
    {
        using var scope = _factory.Services.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var actorId = await CreateTestUserAsync("suggestme");

        var suggestions = await discovery.GetFollowerSuggestionsAsync(actorId);

        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task AddMutedUserAsync_And_GetMutedUsersAsync_Work()
    {
        using var scope = _factory.Services.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var actorA = await CreateTestUserAsync("muter");
        var actorB = await CreateTestUserAsync("mutee");

        await discovery.AddMutedUserAsync(actorA, actorB);

        var muted = await discovery.GetMutedUsersAsync(actorA);
        Assert.Contains(actorB, muted);
    }

    [Fact]
    public async Task RemoveMutedUserAsync_Removes_Muted_User()
    {
        using var scope = _factory.Services.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var actorA = await CreateTestUserAsync("removemuter");
        var actorB = await CreateTestUserAsync("removemutee");

        await discovery.AddMutedUserAsync(actorA, actorB);
        await discovery.RemoveMutedUserAsync(actorA, actorB);

        var muted = await discovery.GetMutedUsersAsync(actorA);
        Assert.DoesNotContain(actorB, muted);
    }

    [Fact]
    public async Task IsMutedAsync_Returns_Correct_State()
    {
        using var scope = _factory.Services.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var actorA = await CreateTestUserAsync("ismuter");
        var actorB = await CreateTestUserAsync("ismutee");

        Assert.False(await discovery.IsMutedAsync(actorA, actorB));

        await discovery.AddMutedUserAsync(actorA, actorB);
        Assert.True(await discovery.IsMutedAsync(actorA, actorB));
    }

    [Fact]
    public async Task AddContentFilterAsync_And_GetContentFiltersAsync_Work()
    {
        using var scope = _factory.Services.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var actorId = await CreateTestUserAsync("filterer");

        await discovery.AddContentFilterAsync(actorId, "spam");
        await discovery.AddContentFilterAsync(actorId, "abuse");

        var filters = await discovery.GetContentFiltersAsync(actorId);
        Assert.Contains("spam", filters);
        Assert.Contains("abuse", filters);
    }

    [Fact]
    public async Task RemoveContentFilterAsync_Removes_Filter()
    {
        using var scope = _factory.Services.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var actorId = await CreateTestUserAsync("removefilterer");

        await discovery.AddContentFilterAsync(actorId, "spam");
        await discovery.RemoveContentFilterAsync(actorId, "spam");

        var filters = await discovery.GetContentFiltersAsync(actorId);
        Assert.DoesNotContain("spam", filters);
    }

    [Fact]
    public async Task IsContentFilteredAsync_Detects_Filtered_Content()
    {
        using var scope = _factory.Services.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var actorId = await CreateTestUserAsync("contentfilterer");

        await discovery.AddContentFilterAsync(actorId, "spam");

        Assert.True(await discovery.IsContentFilteredAsync(actorId, "This is spam content"));
        Assert.False(await discovery.IsContentFilteredAsync(actorId, "This is good content"));
    }

    [Fact]
    public async Task IsContentFilteredAsync_Is_Case_Insensitive()
    {
        using var scope = _factory.Services.CreateScope();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        var actorId = await CreateTestUserAsync("casefilter");

        await discovery.AddContentFilterAsync(actorId, "SPAM");

        Assert.True(await discovery.IsContentFilteredAsync(actorId, "this contains spam"));
        Assert.True(await discovery.IsContentFilteredAsync(actorId, "this contains SPAM"));
        Assert.True(await discovery.IsContentFilteredAsync(actorId, "this contains Spam"));
    }

    [Fact]
    public async Task TrendsController_Returns_200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/trends");

        Assert.True(response.IsSuccessStatusCode);
    }
}
