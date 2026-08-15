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
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ActivityPub.Tests.IntegrationTests.Scale;

public class ActorScaleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActorScaleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ActorScale_CanCreateManyActors()
    {
        const int ActorCount = 50;

        var tasks = new List<Task<Actor>>();
        for (int i = 0; i < ActorCount; i++)
        {
            tasks.Add(CreateTestActorAsync($"scale-actor-{i}"));
        }

        var actors = await Task.WhenAll(tasks);

        Assert.Equal(ActorCount, actors.Length);
    }

    [Fact]
    public async Task ActorScale_CanManageLargeFollowerNetwork()
    {
        var client = _factory.CreateClient();
        var target = await CreateTestActorAsync("network-target");

        var followerTasks = new List<Task>();
        for (int i = 0; i < 30; i++)
        {
            var follower = await CreateTestActorAsync($"network-follower-{i}");
            followerTasks.Add(PostFollowAsync(client, follower, target));
        }

        await Task.WhenAll(followerTasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var followers = await repository.GetFollowersAsync(target.PreferredUsername, 0, 100);

        Assert.Equal(30, followers.Count());
    }

    [Fact]
    public async Task ActorScale_CanHandleManyFollowings()
    {
        var client = _factory.CreateClient();
        var source = await CreateTestActorAsync("following-source");

        var followTasks = new List<Task>();
        for (int i = 0; i < 25; i++)
        {
            var target = await CreateTestActorAsync($"following-target-{i}");
            followTasks.Add(PostFollowAsync(client, source, target));
        }

        await Task.WhenAll(followTasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var followings = await repository.GetFollowingAsync(source.PreferredUsername, 0, 100);

        Assert.Equal(25, followings.Count());
    }

    [Fact]
    public async Task ActorScale_CanMaintainConsistencyAcrossManyActors()
    {
        const int ActorCount = 20;

        var actorTasks = new List<Task<Actor>>();
        for (int i = 0; i < ActorCount; i++)
        {
            actorTasks.Add(CreateTestActorAsync($"consistency-{i}"));
        }

        var actors = await Task.WhenAll(actorTasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        foreach (var actor in actors)
        {
            var storedActor = await repository.GetUserActorAsync(actor.PreferredUsername);
            Assert.NotNull(storedActor);
            Assert.Equal(actor.Id, storedActor.Id);
        }
    }

    [Fact]
    public async Task ActorScale_CanProcessConcurrentActorQueries()
    {
        const int ActorCount = 15;
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);

        var actorTasks = new List<Task<Actor>>();
        for (int i = 0; i < ActorCount; i++)
        {
            actorTasks.Add(CreateTestActorAsync($"concurrent-{testRunId}-{i}"));
        }

        await Task.WhenAll(actorTasks);

        var queryTasks = new List<Task<int>>();
        for (int i = 0; i < 10; i++)
        {
            queryTasks.Add(GetActorCountAsync(testRunId));
        }

        var results = await Task.WhenAll(queryTasks);

        foreach (var count in results)
        {
            Assert.Equal(ActorCount, count);
        }
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

    private async Task<int> GetActorCountAsync(string testRunId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        return await context.Actors.CountAsync(a => a.Username.StartsWith($"concurrent-{testRunId}-"));
    }
}
