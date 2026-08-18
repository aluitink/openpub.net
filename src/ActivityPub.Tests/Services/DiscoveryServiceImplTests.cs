using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DiscoveryServiceImpl"/> (the <c>IDiscoveryService</c>
/// implementation) — trending hashtags, follower suggestions, and the
/// mute / content-filter preferences, which previously had no direct unit
/// test. Uses a real <c>ActivityPubDbContext</c> over the EF Core in-memory
/// provider so the LINQ queries execute against a genuine (offline) database.
/// </summary>
public class DiscoveryServiceImplTests
{
    private readonly ActivityPubDbContext _context;
    private readonly DiscoveryServiceImpl _service;

    public DiscoveryServiceImplTests()
    {
        var options = new DbContextOptionsBuilder<ActivityPubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ActivityPubDbContext(options);
        _service = new DiscoveryServiceImpl(_context);
    }

    private async Task<int> SeedActorAsync(string actorId, string username = "alice")
    {
        var entity = new ActorEntity
        {
            Username = username,
            JsonData = $$"""{"id":"{{actorId}}","type":"Person","preferredUsername":"{{username}}"}""",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Actors.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    private async Task SeedActivityAsync(string jsonData, DateTime? createdAt = null)
    {
        var now = createdAt ?? DateTime.UtcNow;
        _context.Activities.Add(new ActivityEntity
        {
            ActivityId = $"https://pub.example/activities/{Guid.NewGuid():N}",
            JsonData = jsonData,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _context.SaveChangesAsync();
    }

    private static string NoteWithContent(string content) =>
        "{\"type\":\"Create\",\"actor\":\"https://pub.example/users/alice\",\"object\":{\"type\":\"Note\",\"content\":\"" + content + "\"}}";

    private static string FollowActivity(string actorId, string objectId) =>
        "{\"type\":\"Follow\",\"actor\":\"" + actorId + "\",\"object\":\"" + objectId + "\"}";

    // --- Trending hashtags ------------------------------------------------

    [Fact]
    public async Task GetTrendingHashtags_CountsFromContent()
    {
        await SeedActivityAsync(NoteWithContent("Hello #dotnet and #CSharp!"));
        await SeedActivityAsync(NoteWithContent("More #dotnet content"));

        var trending = await _service.GetTrendingHashtagsAsync();

        var dotnet = trending.First(t => t.Tag == "#dotnet");
        var csharp = trending.First(t => t.Tag == "#csharp");

        Assert.Equal(2, dotnet.Count);
        Assert.Equal(1, csharp.Count);
        // Ordered by count desc, then name.
        Assert.Equal("#dotnet", trending.First().Tag);
    }

    [Fact]
    public async Task GetTrendingHashtags_RespectsLimit()
    {
        await SeedActivityAsync(NoteWithContent("#aaa #bbb #ccc"));
        await SeedActivityAsync(NoteWithContent("#ddd #eee #fff"));

        var trending = await _service.GetTrendingHashtagsAsync(limit: 3);
        Assert.Equal(3, trending.Count);
    }

    [Fact]
    public async Task GetTrendingHashtags_RespectsTimeWindow()
    {
        await SeedActivityAsync(NoteWithContent("#old"), createdAt: DateTime.UtcNow.AddHours(-100));
        await SeedActivityAsync(NoteWithContent("#recent"), createdAt: DateTime.UtcNow);

        var all = await _service.GetTrendingHashtagsAsync();
        var windowed = await _service.GetTrendingHashtagsAsync(timeWindow: TimeSpan.FromHours(1));

        Assert.Contains(all, t => t.Tag == "#old");
        Assert.Contains(all, t => t.Tag == "#recent");
        Assert.DoesNotContain(windowed, t => t.Tag == "#old");
        Assert.Contains(windowed, t => t.Tag == "#recent");
    }

    [Fact]
    public async Task GetTrendingHashtags_NoActivities_ReturnsEmpty()
    {
        Assert.Empty(await _service.GetTrendingHashtagsAsync());
    }

    [Fact]
    public async Task GetTrendingHashtags_IgnoresContentWithNoHashtags()
    {
        await SeedActivityAsync(NoteWithContent("plain text, no tags here"));
        Assert.Empty(await _service.GetTrendingHashtagsAsync());
    }

    // --- Follower suggestions --------------------------------------------

    [Fact]
    public async Task GetFollowerSuggestions_ReturnsPopularUnfollowedActors()
    {
        var aliceId = await SeedActorAsync("https://pub.example/users/alice", "alice");
        var bobId = "https://pub.example/users/bob";
        var carolId = "https://pub.example/users/carol";
        var daveId = "https://pub.example/users/dave";
        await SeedActorAsync(bobId, "bob");
        await SeedActorAsync(carolId, "carol");
        await SeedActorAsync(daveId, "dave");

        // Alice already follows Bob.
        await SeedActivityAsync(FollowActivity("https://pub.example/users/alice", bobId));
        // Carol and Dave are followed by others (2 and 1 respectively).
        await SeedActivityAsync(FollowActivity("https://remote.example/users/x", carolId));
        await SeedActivityAsync(FollowActivity("https://remote.example/users/y", carolId));
        await SeedActivityAsync(FollowActivity("https://remote.example/users/z", daveId));

        var suggestions = await _service.GetFollowerSuggestionsAsync("https://pub.example/users/alice");

        // Bob is excluded (already followed); Carol (2 followers) ranks above Dave (1).
        Assert.DoesNotContain(bobId, suggestions);
        Assert.Contains(carolId, suggestions);
        Assert.Contains(daveId, suggestions);
        Assert.Equal(carolId, suggestions.First());
    }

    [Fact]
    public async Task GetFollowerSuggestions_RespectsLimit()
    {
        await SeedActorAsync("https://pub.example/users/alice", "alice");
        for (var i = 0; i < 5; i++)
        {
            var id = $"https://pub.example/users/user{i}";
            await SeedActorAsync(id, $"user{i}");
            await SeedActivityAsync(FollowActivity("https://remote.example/users/r", id));
        }

        var suggestions = await _service.GetFollowerSuggestionsAsync("https://pub.example/users/alice", limit: 2);
        Assert.Equal(2, suggestions.Count);
    }

    [Fact]
    public async Task GetFollowerSuggestions_UnknownUser_ReturnsEmpty()
    {
        Assert.Empty(await _service.GetFollowerSuggestionsAsync("https://pub.example/users/ghost"));
    }

    [Fact]
    public async Task GetFollowerSuggestions_NoData_ReturnsEmpty()
    {
        await SeedActorAsync("https://pub.example/users/alice", "alice");
        Assert.Empty(await _service.GetFollowerSuggestionsAsync("https://pub.example/users/alice"));
    }

    // --- Mutes ------------------------------------------------------------

    [Fact]
    public async Task Mute_Unmute_RoundTrips()
    {
        await SeedActorAsync("https://pub.example/users/alice", "alice");
        var target = "https://pub.example/users/bob";

        await _service.AddMutedUserAsync("https://pub.example/users/alice", target);
        Assert.True(await _service.IsMutedAsync("https://pub.example/users/alice", target));
        Assert.Contains(target, await _service.GetMutedUsersAsync("https://pub.example/users/alice"));

        await _service.RemoveMutedUserAsync("https://pub.example/users/alice", target);
        Assert.False(await _service.IsMutedAsync("https://pub.example/users/alice", target));
        Assert.Empty(await _service.GetMutedUsersAsync("https://pub.example/users/alice"));
    }

    [Fact]
    public async Task AddMutedUser_Idempotent_DoesNotDuplicate()
    {
        await SeedActorAsync("https://pub.example/users/alice", "alice");
        var target = "https://pub.example/users/bob";

        await _service.AddMutedUserAsync("https://pub.example/users/alice", target);
        await _service.AddMutedUserAsync("https://pub.example/users/alice", target);

        var muted = await _service.GetMutedUsersAsync("https://pub.example/users/alice");
        Assert.Equal(1, muted.Count(t => t == target));
    }

    [Fact]
    public async Task IsMutedAsync_UnknownUser_ReturnsFalse()
    {
        Assert.False(await _service.IsMutedAsync("https://pub.example/users/ghost", "https://pub.example/users/bob"));
    }

    [Fact]
    public async Task GetMutedUsers_UnknownUser_ReturnsEmpty()
    {
        Assert.Empty(await _service.GetMutedUsersAsync("https://pub.example/users/ghost"));
    }

    // --- Content filters --------------------------------------------------

    [Fact]
    public async Task AddFilter_IsContentFiltered_RoundTrips()
    {
        await SeedActorAsync("https://pub.example/users/alice", "alice");
        var userId = "https://pub.example/users/alice";

        await _service.AddContentFilterAsync(userId, "SPAM!");
        Assert.True(await _service.IsContentFilteredAsync(userId, "buy now, SPAM! deals"));
        Assert.False(await _service.IsContentFilteredAsync(userId, "totally clean text"));
        Assert.Contains("spam!", await _service.GetContentFiltersAsync(userId));

        await _service.RemoveContentFilterAsync(userId, "spam!");
        Assert.False(await _service.IsContentFilteredAsync(userId, "buy now, spam! deals"));
        Assert.Empty(await _service.GetContentFiltersAsync(userId));
    }

    [Fact]
    public async Task AddContentFilter_Idempotent_DoesNotDuplicate()
    {
        await SeedActorAsync("https://pub.example/users/alice", "alice");
        var userId = "https://pub.example/users/alice";

        await _service.AddContentFilterAsync(userId, "badword");
        await _service.AddContentFilterAsync(userId, "badword");

        var filters = await _service.GetContentFiltersAsync(userId);
        Assert.Equal(1, filters.Count(f => f == "badword"));
    }

    [Fact]
    public async Task IsContentFiltered_UnknownUser_ReturnsFalse()
    {
        Assert.False(await _service.IsContentFilteredAsync("https://pub.example/users/ghost", "anything"));
    }

    [Fact]
    public async Task GetContentFilters_UnknownUser_ReturnsEmpty()
    {
        Assert.Empty(await _service.GetContentFiltersAsync("https://pub.example/users/ghost"));
    }
}
