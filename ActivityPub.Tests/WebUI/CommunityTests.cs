using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using ActivityPub.Core.Repositories;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class CommunityTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public CommunityTests(WebUIFactory factory)
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
    public async Task CreateCommunityAsync_Creates_And_Returns_Community()
    {
        using var scope = _factory.Services.CreateScope();
        var community = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("communityowner");

        var result = await community.CreateCommunityAsync(ownerId, "DotNet Devs", "A community for .NET developers");

        Assert.NotNull(result);
        Assert.Equal("DotNet Devs", result.Name);
        Assert.Equal("Group", result.Type);
        Assert.Equal("A community for .NET developers", result.Summary);
    }

    [Fact]
    public async Task CreateCommunityAsync_Makes_Owner_Automatic_Member()
    {
        using var scope = _factory.Services.CreateScope();
        var community = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("autocommunityowner");

        var result = await community.CreateCommunityAsync(ownerId, "Auto Member Test", null);

        Assert.NotNull(result);
        Assert.True(await community.IsMemberAsync(ownerId, result.Id));
        Assert.Equal(1, await community.GetMemberCountAsync(result.Id));
    }

    [Fact]
    public async Task JoinCommunityAsync_Adds_Member()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("joinowner");
        var memberId = await CreateTestUserAsync("joinmember");

        var community = await communityService.CreateCommunityAsync(ownerId, "Join Test", null);
        Assert.NotNull(community);

        var joined = await communityService.JoinCommunityAsync(memberId, community.Id);

        Assert.True(joined);
        Assert.True(await communityService.IsMemberAsync(memberId, community.Id));
        Assert.Equal(2, await communityService.GetMemberCountAsync(community.Id));
    }

    [Fact]
    public async Task LeaveCommunityAsync_Removes_Member()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("leaveowner");
        var memberId = await CreateTestUserAsync("leavemember");

        var community = await communityService.CreateCommunityAsync(ownerId, "Leave Test", null);
        Assert.NotNull(community);

        await communityService.JoinCommunityAsync(memberId, community.Id);
        var left = await communityService.LeaveCommunityAsync(memberId, community.Id);

        Assert.True(left);
        Assert.False(await communityService.IsMemberAsync(memberId, community.Id));
        Assert.Equal(1, await communityService.GetMemberCountAsync(community.Id));
    }

    [Fact]
    public async Task GetMyCommunitiesAsync_Returns_User_Communities()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var userId = await CreateTestUserAsync("mycommunities");

        var c1 = await communityService.CreateCommunityAsync(userId, "My Community 1", null);
        var c2 = await communityService.CreateCommunityAsync(userId, "My Community 2", null);
        Assert.NotNull(c1);
        Assert.NotNull(c2);

        var communities = await communityService.GetMyCommunitiesAsync(userId);

        Assert.Equal(2, communities.Count);
    }

    [Fact]
    public async Task SearchCommunitiesAsync_Finds_By_Name()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("searchowner");

        await communityService.CreateCommunityAsync(ownerId, "Python Devs", null);
        await communityService.CreateCommunityAsync(ownerId, "Rust Devs", null);
        await communityService.CreateCommunityAsync(ownerId, "Go Devs", null);

        var results = await communityService.SearchCommunitiesAsync("python");

        Assert.Single(results);
        Assert.Equal("Python Devs", results.First().Name);
    }

    [Fact]
    public async Task GetMemberIdsAsync_Returns_All_Member_Ids()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("memberowner");
        var member1 = await CreateTestUserAsync("member1");
        var member2 = await CreateTestUserAsync("member2");

        var community = await communityService.CreateCommunityAsync(ownerId, "Members Test", null);
        Assert.NotNull(community);

        await communityService.JoinCommunityAsync(member1, community.Id);
        await communityService.JoinCommunityAsync(member2, community.Id);

        var memberIds = await communityService.GetMemberIdsAsync(community.Id);

        Assert.Equal(3, memberIds.Count);
        Assert.Contains(ownerId, memberIds);
        Assert.Contains(member1, memberIds);
        Assert.Contains(member2, memberIds);
    }

    [Fact]
    public async Task DeleteCommunityAsync_Removes_Community()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("deleteowner");

        var community = await communityService.CreateCommunityAsync(ownerId, "To Delete", null);
        Assert.NotNull(community);

        var deleted = await communityService.DeleteCommunityAsync(ownerId, community.Id);

        Assert.True(deleted);
        Assert.Null(await communityService.GetCommunityByIdAsync(community.Id));
    }

    [Fact]
    public async Task GetAllCommunitiesAsync_Returns_Communities()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("allcommowner");

        var c1 = await communityService.CreateCommunityAsync(ownerId, "All Comm 1", null);
        var c2 = await communityService.CreateCommunityAsync(ownerId, "All Comm 2", null);
        Assert.NotNull(c1);
        Assert.NotNull(c2);

        var communities = await communityService.GetAllCommunitiesAsync();

        Assert.NotEmpty(communities);
    }

    [Fact]
    public async Task GetAllCommunitiesAsync_Supports_Pagination()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("paginateowner");

        await communityService.CreateCommunityAsync(ownerId, "Paginate 1", null);
        await communityService.CreateCommunityAsync(ownerId, "Paginate 2", null);

        var page1 = await communityService.GetAllCommunitiesAsync(skip: 0, take: 1);

        Assert.Single(page1);
    }

    [Fact]
    public async Task CreateCommunityAsync_Returns_Null_For_Unknown_Owner()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var result = await communityService.CreateCommunityAsync("https://localhost/users/nonexistent", "Ghost Community", null);

        Assert.Null(result);
    }

    [Fact]
    public async Task JoinCommunityAsync_When_Already_Member_Returns_True()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("idempotentowner");
        var memberId = await CreateTestUserAsync("idempotentmember");

        var community = await communityService.CreateCommunityAsync(ownerId, "Idempotent Test", null);
        Assert.NotNull(community);

        await communityService.JoinCommunityAsync(memberId, community.Id);
        var joinedAgain = await communityService.JoinCommunityAsync(memberId, community.Id);

        Assert.True(joinedAgain);
        Assert.Equal(2, await communityService.GetMemberCountAsync(community.Id));
    }

    [Fact]
    public async Task LeaveCommunityAsync_When_Not_Member_Returns_False()
    {
        using var scope = _factory.Services.CreateScope();
        var communityService = scope.ServiceProvider.GetRequiredService<ICommunityService>();

        var ownerId = await CreateTestUserAsync("notmemberowner");
        var memberId = await CreateTestUserAsync("notmemberuser");

        var community = await communityService.CreateCommunityAsync(ownerId, "Not Member Test", null);
        Assert.NotNull(community);

        var left = await communityService.LeaveCommunityAsync(memberId, community.Id);

        Assert.False(left);
    }
}
