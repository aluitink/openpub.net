using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.IntegrationTests.Performance;

public class PerformanceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PerformanceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Performance_CanCreateManyActivities()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("perf-actor-1");

        for (int i = 0; i < 50; i++)
        {
            var activity = new Activity
            {
                Id = $"https://localhost/users/{actor.PreferredUsername}/activities/perf-{i}",
                Type = "Create",
                Actor = actor.Id,
                Object = new Note
                {
                    Id = $"https://localhost/users/{actor.PreferredUsername}/notes/perf-{i}",
                    Type = "Note",
                    Content = $"Performance test activity {i}"
                },
                Published = DateTime.UtcNow
            };

            var content = CreateActivityContent(activity);

            var response = await client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var activities = await GetActorActivitiesAsync(actor.PreferredUsername ?? string.Empty);
        Assert.Equal(50, activities.Count);
    }

    [Fact]
    public async Task Performance_CanCreateManyFollowers()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("perf-follow-target");

        for (int i = 0; i < 30; i++)
        {
            var follower = await CreateTestActorAsync($"perf-follower-{i}");
            var followActivity = new Activity
            {
                Id = $"https://localhost/users/perf-follower-{i}/activities/follow-{i}",
                Type = "Follow",
                Actor = follower.Id,
                Object = actor.Id,
                Published = DateTime.UtcNow
            };

            var content = CreateActivityContent(followActivity);

            var response = await client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var followers = await GetFollowersAsync(actor.PreferredUsername ?? string.Empty);
        Assert.True(followers.Count >= 30);
    }

    [Fact]
    public async Task Performance_CanRetrieveManyActivities()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("perf-retrieve");

        for (int i = 0; i < 25; i++)
        {
            var activity = new Activity
            {
                Id = $"https://localhost/users/{actor.PreferredUsername}/activities/ret-{i}",
                Type = "Create",
                Actor = actor.Id,
                Object = new Note
                {
                    Id = $"https://localhost/users/{actor.PreferredUsername}/notes/ret-{i}",
                    Type = "Note",
                    Content = $"Retrieval test {i}"
                },
                Published = DateTime.UtcNow
            };

            var content = CreateActivityContent(activity);

            await client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
        }

        var activities = await GetActorActivitiesAsync(actor.PreferredUsername ?? string.Empty);
        Assert.Equal(25, activities.Count);
    }

    [Fact]
    public async Task Performance_ConcurrentActivityRetrieval()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("perf-concurrent");

        for (int i = 0; i < 20; i++)
        {
            var activity = new Activity
            {
                Id = $"https://localhost/users/{actor.PreferredUsername}/activities/conc-{i}",
                Type = "Create",
                Actor = actor.Id,
                Object = new Note
                {
                    Id = $"https://localhost/users/{actor.PreferredUsername}/notes/conc-{i}",
                    Type = "Note",
                    Content = $"Concurrent test {i}"
                },
                Published = DateTime.UtcNow
            };

            var content = CreateActivityContent(activity);

            await client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
        }

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            return await GetActorActivitiesAsync(actor.PreferredUsername ?? string.Empty);
        });

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            Assert.Equal(20, result.Count);
        }
    }

    [Fact]
    public async Task Performance_LargeActivityPayload()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("perf-large");

        var largeContent = new string('x', 10000);
        var activity = new Activity
        {
            Id = $"https://localhost/users/{actor.PreferredUsername}/activities/large",
            Type = "Create",
            Actor = actor.Id,
            Object = new Note
            {
                Id = $"https://localhost/users/{actor.PreferredUsername}/notes/large",
                Type = "Note",
                Content = largeContent
            },
            Published = DateTime.UtcNow
        };

        var content = CreateActivityContent(activity);

        var response = await client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var activities = await GetActorActivitiesAsync(actor.PreferredUsername ?? string.Empty);
        Assert.Single(activities);
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

    private StringContent CreateActivityContent(Activity activity)
    {
        var json = global::System.Text.Json.JsonSerializer.Serialize(activity, new global::System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = global::System.Text.Json.JsonNamingPolicy.CamelCase });
        return new StringContent(json, Encoding.UTF8, "application/activity+json");
    }

    private async Task<List<Activity>> GetActorActivitiesAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var activities = await repository.GetActorOutboxActivitiesAsync(username, 0, 100);

        var result = new List<Activity>();
        foreach (var activityId in activities)
        {
            var activity = await repository.GetActivityAsync(activityId);
            if (activity != null)
            {
                result.Add(activity);
            }
        }
        return result;
    }

    private async Task<List<string>> GetFollowersAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var followers = await repository.GetFollowersAsync(username, 0, 100);
        return followers.ToList();
    }
}
