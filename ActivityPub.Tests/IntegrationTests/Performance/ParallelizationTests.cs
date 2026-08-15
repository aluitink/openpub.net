using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Tests;
using Microsoft.Extensions.DependencyInjection;
using ActivityPub.Core;

namespace ActivityPub.Tests.IntegrationTests.Performance;

public class ParallelizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ParallelizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Parallelization_MultipleActorsCanPostSimultaneously()
    {
        var client = _factory.CreateClient();
        var actor1 = await CreateTestActorAsync("parallel-actor-1");
        var actor2 = await CreateTestActorAsync("parallel-actor-2");
        var actor3 = await CreateTestActorAsync("parallel-actor-3");

        var tasks = new List<Task>
        {
            PostActivityAsync(actor1.PreferredUsername, "parallel-1"),
            PostActivityAsync(actor2.PreferredUsername, "parallel-2"),
            PostActivityAsync(actor3.PreferredUsername, "parallel-3")
        };

        await Task.WhenAll(tasks);

        var activities1 = await GetActorActivitiesAsync(actor1.PreferredUsername);
        var activities2 = await GetActorActivitiesAsync(actor2.PreferredUsername);
        var activities3 = await GetActorActivitiesAsync(actor3.PreferredUsername);

        Assert.Single(activities1);
        Assert.Single(activities2);
        Assert.Single(activities3);
    }

    [Fact]
    public async Task Parallelization_MultipleFollowersCanPostSimultaneously()
    {
        var client = _factory.CreateClient();
        var target = await CreateTestActorAsync($"parallel-target-{Guid.NewGuid():N}");

        var tasks = new List<Task>();
        for (int i = 0; i < 15; i++)
        {
            var follower = await CreateTestActorAsync($"parallel-follower-{Guid.NewGuid():N}");
            tasks.Add(PostFollowAsync(client, follower, target));
        }

        await Task.WhenAll(tasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var followers = await repository.GetFollowersAsync(target.PreferredUsername, 0, 100);
        Assert.True(followers.Count() >= 15);
    }

    [Fact]
    public async Task Parallelization_ConcurrentActivityRetrieval()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("parallel-retrieve");

        for (int i = 0; i < 10; i++)
        {
            await PostActivityAsync(actor.PreferredUsername, $"parallel-retrieve-{i}");
        }

        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            return await GetActorActivitiesAsync(actor.PreferredUsername);
        });

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            Assert.Equal(10, result.Count);
        }
    }

    [Fact]
    public async Task Parallelization_HighConcurrencyLoad()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("parallel-highload");

        const int ConcurrentBatches = 5;
        const int ActivitiesPerBatch = 10;

        var allTasks = new List<Task>();

        for (int batch = 0; batch < ConcurrentBatches; batch++)
        {
            for (int i = 0; i < ActivitiesPerBatch; i++)
            {
                allTasks.Add(PostActivityAsync(actor.PreferredUsername, $"batch{batch}-item{i}"));
            }
        }

        await Task.WhenAll(allTasks);

        var activities = await GetActorActivitiesAsync(actor.PreferredUsername);
        Assert.Equal(ConcurrentBatches * ActivitiesPerBatch, activities.Count);
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

    private async Task PostActivityAsync(string username, string activityContent)
    {
        var activity = new Activity
        {
            Id = $"https://localhost/users/{username}/activities/par-{Guid.NewGuid()}",
            Type = "Create",
            Actor = $"https://localhost/users/{username}",
            Object = new Note
            {
                Id = $"https://localhost/users/{username}/notes/par-{Guid.NewGuid()}",
                Type = "Note",
                Content = activityContent
            },
            Published = DateTime.UtcNow
        };

        var content = CreateActivityContent(activity);

        var client = _factory.CreateClient();
        await client.PostAsync($"/users/{username}/inbox", content);
    }

    private async Task PostFollowAsync(HttpClient client, Actor follower, Actor target)
    {
        var followActivity = new Activity
        {
            Id = $"https://localhost/users/{follower.PreferredUsername}/activities/follow-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Type = "Follow",
            Actor = follower.Id,
            Object = target.Id,
            Published = DateTime.UtcNow
        };

        var content = CreateActivityContent(followActivity);
        await client.PostAsync($"/users/{target.PreferredUsername}/inbox", content);
    }

    private StringContent CreateActivityContent(Activity activity)
    {
        var json = global::System.Text.Json.JsonSerializer.Serialize(activity, new global::System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = global::System.Text.Json.JsonNamingPolicy.CamelCase });
        return new StringContent(json, Encoding.UTF8, "application/activity+json");
    }

    private async Task<List<Activity>> GetActorActivitiesAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var activityIds = await repository.GetActorOutboxActivitiesAsync(username, 0, 100);

        var result = new List<Activity>();
        foreach (var activityId in activityIds)
        {
            var activity = await repository.GetActivityAsync(activityId);
            if (activity != null)
            {
                result.Add(activity);
            }
        }
        return result;
    }

}
