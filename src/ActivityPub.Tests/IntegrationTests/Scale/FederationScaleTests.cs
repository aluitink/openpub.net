using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.IntegrationTests.Scale;

public class FederationScaleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public FederationScaleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FederationScale_CanHandleManyOutboundFollows()
    {
        var client = _factory.CreateClient();
        var source = await CreateTestActorAsync("federation-source");

        var followTasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var target = await CreateTestActorAsync($"federation-target-{i}");
            followTasks.Add(PostFollowAsync(client, source, target));
        }

        await Task.WhenAll(followTasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var followings = await repository.GetFollowingAsync(source.PreferredUsername ?? string.Empty, 0, 100);

        Assert.Equal(10, followings.Count());
    }

    [Fact]
    public async Task FederationScale_CanHandleManyInboundFollows()
    {
        var client = _factory.CreateClient();
        var target = await CreateTestActorAsync("federation-target");

        var followTasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            var follower = await CreateTestActorAsync($"federation-follower-{i}");
            followTasks.Add(PostFollowAsync(client, follower, target));
        }

        await Task.WhenAll(followTasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var followers = await repository.GetFollowersAsync(target.PreferredUsername ?? string.Empty, 0, 100);

        Assert.Equal(20, followers.Count());
    }

    [Fact]
    public async Task FederationScale_CanHandleBidirectionalFollows()
    {
        var client = _factory.CreateClient();
        var actor1 = await CreateTestActorAsync("bidi-actor1");
        var actor2 = await CreateTestActorAsync("bidi-actor2");

        await PostFollowAsync(client, actor1, actor2);
        await PostFollowAsync(client, actor2, actor1);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var following1 = await repository.GetFollowingAsync(actor1.PreferredUsername ?? string.Empty, 0, 100);
        var following2 = await repository.GetFollowingAsync(actor2.PreferredUsername ?? string.Empty, 0, 100);

        Assert.Single(following1);
        Assert.Single(following2);
    }

    [Fact]
    public async Task FederationScale_CanHandleMultipleFollowerCircles()
    {
        var client = _factory.CreateClient();

        var circle1Target = await CreateTestActorAsync("circle1-target");
        var circle2Target = await CreateTestActorAsync("circle2-target");

        for (int i = 0; i < 10; i++)
        {
            var follower1 = await CreateTestActorAsync($"circle1-follower-{i}");
            var follower2 = await CreateTestActorAsync($"circle2-follower-{i}");

            await PostFollowAsync(client, follower1, circle1Target);
            await PostFollowAsync(client, follower2, circle2Target);
        }

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var circle1Followers = await repository.GetFollowersAsync(circle1Target.PreferredUsername ?? string.Empty, 0, 100);
        var circle2Followers = await repository.GetFollowersAsync(circle2Target.PreferredUsername ?? string.Empty, 0, 100);

        Assert.Equal(10, circle1Followers.Count());
        Assert.Equal(10, circle2Followers.Count());
    }

    [Fact]
    public async Task FederationScale_CanMaintainFollowerCountConsistency()
    {
        var client = _factory.CreateClient();
        var target = await CreateTestActorAsync("consistency-target");

        var followTasks = new List<Task>();
        for (int i = 0; i < 15; i++)
        {
            var follower = await CreateTestActorAsync($"consistency-follower-{i}");
            followTasks.Add(PostFollowAsync(client, follower, target));
        }

        await Task.WhenAll(followTasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var followers = await repository.GetFollowersAsync(target.PreferredUsername ?? string.Empty, 0, 100);
        var followerCount = followers.Count();

        Assert.Equal(15, followerCount);
    }

    private async Task<Actor> CreateTestActorAsync(string username)
    {
        var actor = new Actor
        {
            Id = $"https://localhost/users/{username}",
            Type = "Person",
            PreferredUsername = username,
            Inbox = $"https://localhost/users/{username}/inbox",
            Outbox = $"https://localhost/users/{username}/outbox"
        };

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        await repository.SaveUserActorAsync(actor);

        return actor;
    }

    private async Task PostFollowAsync(HttpClient client, Actor follower, Actor target)
    {
        var followActivity = new Activity
        {
            Id = $"https://localhost/users/{follower.PreferredUsername}/activities/follow-{Guid.NewGuid()}",
            Type = "Follow",
            Actor = follower.Id,
            Object = target.Id,
            Published = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(followActivity, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/activity+json"));

        await client.PostAsync($"/users/{target.PreferredUsername}/inbox", content);
    }
}
