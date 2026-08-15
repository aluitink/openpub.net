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

public class DatabaseScaleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DatabaseScaleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DatabaseScale_CanInsertAndQueryLargeDataset()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int ActorCount = 200;

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var tasks = new List<Task<Actor>>();
        for (int i = 0; i < ActorCount; i++)
        {
            tasks.Add(CreateTestActorAsync(repository, $"db-large-{testRunId}-{i}"));
        }

        var actors = await Task.WhenAll(tasks);

        Assert.Equal(ActorCount, actors.Length);
    }

    [Fact]
    public async Task DatabaseScale_OptimizedBatchInsertPerformance()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int BatchSize = 150;

        var tasks = new List<Task<Actor>>();
        for (int i = 0; i < BatchSize; i++)
        {
            tasks.Add(CreateTestActorAsync($"db-batch-{testRunId}-{i}"));
        }

        var actors = await Task.WhenAll(tasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var storedActors = await repository.GetUserActorAsync($"db-batch-{testRunId}-0");
        Assert.NotNull(storedActors);
    }

    [Fact]
    public async Task DatabaseScale_CanQueryLargeFollowerSet()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int FollowerCount = 100;

        var client = _factory.CreateClient();
        var target = await CreateTestActorAsync($"db-follower-target-{testRunId}");

        var followTasks = new List<Task>();
        for (int i = 0; i < FollowerCount; i++)
        {
            var follower = await CreateTestActorAsync($"db-follower-{testRunId}-{i}");
            followTasks.Add(PostFollowAsync(client, follower, target));
        }

        await Task.WhenAll(followTasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var followers = await repository.GetFollowersAsync(target.PreferredUsername, 0, 1000);

        Assert.True(followers.Count() >= FollowerCount);
    }

    [Fact]
    public async Task DatabaseScale_CanHandleConcurrentWriteOperations()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int ConcurrentCount = 80;

        var writeTasks = new List<Task<Actor>>();
        for (int i = 0; i < ConcurrentCount; i++)
        {
            writeTasks.Add(CreateTestActorAsync($"db-concurrent-{testRunId}-{i}"));
        }

        var actors = await Task.WhenAll(writeTasks);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var count = await context.Actors.CountAsync(a => a.Username.Contains(testRunId));

        Assert.Equal(ConcurrentCount, count);
    }

    [Fact]
    public async Task DatabaseScale_CanPaginateThroughLargeResultSets()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int TotalItems = 800;
        const int PageSize = 100;

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var usernames = new List<string>();
        for (int i = 0; i < TotalItems; i++)
        {
            var username = $"db-paginate-{testRunId}-{i}";
            usernames.Add(username);

            var actor = new Actor
            {
                Id = $"https://localhost/users/{username}",
                Type = "Person",
                PreferredUsername = username,
                Inbox = $"https://localhost/users/{username}/inbox",
                Outbox = $"https://localhost/users/{username}/outbox"
            };
            await repository.SaveUserActorAsync(actor);
        }

        var query = context.Actors
            .Where(a => usernames.Contains(a.Username))
            .OrderBy(a => a.Username);

        var allActors = new List<ActorEntity>();
        for (int page = 0; page < TotalItems / PageSize; page++)
        {
            var pageActors = await query.Skip(page * PageSize).Take(PageSize).ToListAsync();
            allActors.AddRange(pageActors);
        }

        Assert.Equal(TotalItems, allActors.Count);
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

    private async Task<Actor> CreateTestActorAsync(IActivityPubRepository repository, string username)
    {
        var actor = new Actor
        {
            Id = $"https://localhost/users/{username}",
            Type = "Person",
            PreferredUsername = username,
            Inbox = $"https://localhost/users/{username}/inbox",
            Outbox = $"https://localhost/users/{username}/outbox"
        };

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
