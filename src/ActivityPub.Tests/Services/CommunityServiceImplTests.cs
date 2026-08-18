using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CommunityServiceImpl"/> (the <c>ICommunityService</c>
/// implementation) — the community/group CRUD + membership surface, which
/// previously had no direct unit test. Uses a real <c>ActivityPubDbContext</c>
/// over the EF Core in-memory provider so the LINQ queries execute against a
/// genuine (offline) database.
/// </summary>
public class CommunityServiceImplTests
{
    private readonly ActivityPubDbContext _context;
    private readonly CommunityServiceImpl _service;

    public CommunityServiceImplTests()
    {
        var options = new DbContextOptionsBuilder<ActivityPubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ActivityPubDbContext(options);
        _service = new CommunityServiceImpl(_context);
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

    private const string OwnerId = "https://pub.example/users/alice";

    // --- Create / Get ----------------------------------------------------

    [Fact]
    public async Task CreateCommunity_KnownOwner_CreatesCommunityAndOwnerMembership()
    {
        await SeedActorAsync(OwnerId);

        var community = await _service.CreateCommunityAsync(OwnerId, "Book Club", "We read together");

        Assert.NotNull(community);
        Assert.Equal("Group", community!.Type);
        Assert.Equal("Book Club", community.Name);
        Assert.Equal("https://pub.example/communities/book-club", community.Id);
        Assert.Equal(OwnerId, community.OwnerId);

        // Owner is auto-added as a member.
        Assert.True(await _service.IsMemberAsync(OwnerId, community.Id));
    }

    [Fact]
    public async Task CreateCommunity_UnknownOwner_ReturnsNull()
    {
        var community = await _service.CreateCommunityAsync("https://pub.example/users/ghost", "Ghost Group", null);
        Assert.Null(community);
    }

    [Fact]
    public async Task GetCommunityById_Existing_ReturnsCommunity()
    {
        await SeedActorAsync(OwnerId);
        var created = await _service.CreateCommunityAsync(OwnerId, "Art Group", "Painting");
        Assert.NotNull(created);

        var fetched = await _service.GetCommunityByIdAsync(created!.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Art Group", fetched!.Name);
        Assert.Equal("Painting", fetched.Summary);
    }

    [Fact]
    public async Task GetCommunityById_Missing_ReturnsNull()
    {
        Assert.Null(await _service.GetCommunityByIdAsync("https://pub.example/communities/nope"));
    }

    [Fact]
    public async Task UpdateCommunity_Existing_PersistsChanges()
    {
        await SeedActorAsync(OwnerId);
        var created = await _service.CreateCommunityAsync(OwnerId, "Old Name", "old");
        Assert.NotNull(created);

        created!.Name = "New Name";
        created.Summary = "updated summary";
        Assert.True(await _service.UpdateCommunityAsync(created));

        var fetched = await _service.GetCommunityByIdAsync(created.Id);
        Assert.Equal("New Name", fetched!.Name);
        Assert.Equal("updated summary", fetched.Summary);
    }

    [Fact]
    public async Task UpdateCommunity_Missing_ReturnsFalse()
    {
        var community = new Community { Id = "https://pub.example/communities/nope", Type = "Group", Name = "X" };
        Assert.False(await _service.UpdateCommunityAsync(community));
    }

    // --- List / Search ---------------------------------------------------

    [Fact]
    public async Task GetAllCommunities_ReturnsCreated()
    {
        await SeedActorAsync(OwnerId);
        await _service.CreateCommunityAsync(OwnerId, "Alpha", null);
        await _service.CreateCommunityAsync(OwnerId, "Beta", null);

        var all = await _service.GetAllCommunitiesAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetAllCommunities_RespectsSkipTake()
    {
        await SeedActorAsync(OwnerId);
        for (var i = 0; i < 5; i++)
            await _service.CreateCommunityAsync(OwnerId, $"Group {i}", null);

        var page = await _service.GetAllCommunitiesAsync(skip: 0, take: 2);
        Assert.Equal(2, page.Count);
    }

    [Fact]
    public async Task SearchCommunities_MatchesByName_CaseInsensitive()
    {
        await SeedActorAsync(OwnerId);
        await _service.CreateCommunityAsync(OwnerId, "Poetry Night", null);
        await _service.CreateCommunityAsync(OwnerId, "Chess Club", null);

        var results = await _service.SearchCommunitiesAsync("poetry");
        Assert.Single(results);
        Assert.Equal("Poetry Night", results.First().Name);
    }

    [Fact]
    public async Task SearchCommunities_NoMatch_ReturnsEmpty()
    {
        await SeedActorAsync(OwnerId);
        await _service.CreateCommunityAsync(OwnerId, "Poetry Night", null);

        Assert.Empty(await _service.SearchCommunitiesAsync("nonexistent"));
    }

    // --- Membership ------------------------------------------------------

    [Fact]
    public async Task JoinCommunity_KnownActorAndCommunity_AddsMember()
    {
        var ownerId = await SeedActorAsync(OwnerId);
        var memberId = await SeedActorAsync("https://pub.example/users/bob", "bob");

        var community = await _service.CreateCommunityAsync(OwnerId, "Book Club", null);
        Assert.NotNull(community);

        Assert.True(await _service.JoinCommunityAsync("https://pub.example/users/bob", community!.Id));
        Assert.True(await _service.IsMemberAsync("https://pub.example/users/bob", community.Id));
    }

    [Fact]
    public async Task JoinCommunity_Idempotent_SecondJoinStillTrue()
    {
        await SeedActorAsync(OwnerId);
        var community = await _service.CreateCommunityAsync(OwnerId, "Book Club", null);
        Assert.NotNull(community);

        Assert.True(await _service.JoinCommunityAsync(OwnerId, community!.Id));
        Assert.True(await _service.JoinCommunityAsync(OwnerId, community.Id));
        Assert.Equal(1, await _service.GetMemberCountAsync(community.Id));
    }

    [Fact]
    public async Task JoinCommunity_UnknownActorOrCommunity_ReturnsFalse()
    {
        await SeedActorAsync(OwnerId);
        var community = await _service.CreateCommunityAsync(OwnerId, "Book Club", null);
        Assert.NotNull(community);

        Assert.False(await _service.JoinCommunityAsync("https://pub.example/users/ghost", community!.Id));
        Assert.False(await _service.JoinCommunityAsync(OwnerId, "https://pub.example/communities/nope"));
    }

    [Fact]
    public async Task LeaveCommunity_Member_RemovesMembership()
    {
        await SeedActorAsync(OwnerId);
        await SeedActorAsync("https://pub.example/users/bob", "bob");
        var community = await _service.CreateCommunityAsync(OwnerId, "Book Club", null);
        Assert.NotNull(community);
        await _service.JoinCommunityAsync("https://pub.example/users/bob", community!.Id);
        Assert.True(await _service.IsMemberAsync("https://pub.example/users/bob", community.Id));

        Assert.True(await _service.LeaveCommunityAsync("https://pub.example/users/bob", community.Id));
        Assert.False(await _service.IsMemberAsync("https://pub.example/users/bob", community.Id));
    }

    [Fact]
    public async Task LeaveCommunity_NotMember_ReturnsFalse()
    {
        // Bob's group — the owner (Bob) is a member, but Alice is not.
        await SeedActorAsync(OwnerId);
        await SeedActorAsync("https://pub.example/users/bob", "bob");
        var community = await _service.CreateCommunityAsync("https://pub.example/users/bob", "Bob's Group", null);
        Assert.NotNull(community);

        Assert.False(await _service.LeaveCommunityAsync(OwnerId, community!.Id));
    }

    [Fact]
    public async Task IsMemberAsync_UnknownActorOrCommunity_ReturnsFalse()
    {
        Assert.False(await _service.IsMemberAsync("https://pub.example/users/ghost", "https://pub.example/communities/nope"));
    }

    [Fact]
    public async Task GetMemberIdsAndCount_ReflectMembers()
    {
        await SeedActorAsync(OwnerId);
        await SeedActorAsync("https://pub.example/users/bob", "bob");
        var community = await _service.CreateCommunityAsync(OwnerId, "Book Club", null);
        Assert.NotNull(community);
        await _service.JoinCommunityAsync("https://pub.example/users/bob", community!.Id);

        Assert.Equal(2, await _service.GetMemberCountAsync(community.Id));

        var ids = await _service.GetMemberIdsAsync(community.Id);
        Assert.Contains(OwnerId, ids);
        Assert.Contains("https://pub.example/users/bob", ids);
    }

    [Fact]
    public async Task GetMemberIdsAndCount_UnknownCommunity_ReturnsEmpty()
    {
        Assert.Empty(await _service.GetMemberIdsAsync("https://pub.example/communities/nope"));
        Assert.Equal(0, await _service.GetMemberCountAsync("https://pub.example/communities/nope"));
    }

    // --- My communities --------------------------------------------------

    [Fact]
    public async Task GetMyCommunities_ReturnsCommunitiesUserJoined()
    {
        await SeedActorAsync(OwnerId);
        await SeedActorAsync("https://pub.example/users/bob", "bob");

        var c1 = await _service.CreateCommunityAsync(OwnerId, "Owned", null);
        var c2 = await _service.CreateCommunityAsync("https://pub.example/users/bob", "Bob's Group", null);
        await _service.JoinCommunityAsync(OwnerId, c2!.Id);
        Assert.NotNull(c1);

        var mine = await _service.GetMyCommunitiesAsync(OwnerId);
        var names = mine.Select(c => c.Name).ToList();

        Assert.Contains("Owned", names);
        Assert.Contains("Bob's Group", names);
        Assert.Equal(2, mine.Count);
    }

    [Fact]
    public async Task GetMyCommunities_UnknownActor_ReturnsEmpty()
    {
        Assert.Empty(await _service.GetMyCommunitiesAsync("https://pub.example/users/ghost"));
    }

    // --- Delete ----------------------------------------------------------

    [Fact]
    public async Task DeleteCommunity_Owner_RemovesCommunityAndMembers()
    {
        await SeedActorAsync(OwnerId);
        var community = await _service.CreateCommunityAsync(OwnerId, "Book Club", null);
        Assert.NotNull(community);
        await SeedActorAsync("https://pub.example/users/bob", "bob");
        await _service.JoinCommunityAsync("https://pub.example/users/bob", community!.Id);

        Assert.True(await _service.DeleteCommunityAsync(OwnerId, community.Id));
        Assert.Null(await _service.GetCommunityByIdAsync(community.Id));
        Assert.Equal(0, await _service.GetMemberCountAsync(community.Id));
    }

    [Fact]
    public async Task DeleteCommunity_NotOwner_ReturnsFalse()
    {
        var ownerId = await SeedActorAsync(OwnerId);
        var otherId = await SeedActorAsync("https://pub.example/users/bob", "bob");
        var community = await _service.CreateCommunityAsync(OwnerId, "Book Club", null);
        Assert.NotNull(community);

        Assert.False(await _service.DeleteCommunityAsync("https://pub.example/users/bob", community!.Id));
        Assert.NotNull(await _service.GetCommunityByIdAsync(community.Id));
    }

    [Fact]
    public async Task DeleteCommunity_UnknownOwner_ReturnsFalse()
    {
        await SeedActorAsync(OwnerId);
        var community = await _service.CreateCommunityAsync(OwnerId, "Book Club", null);
        Assert.NotNull(community);

        Assert.False(await _service.DeleteCommunityAsync("https://pub.example/users/ghost", community!.Id));
    }
}
