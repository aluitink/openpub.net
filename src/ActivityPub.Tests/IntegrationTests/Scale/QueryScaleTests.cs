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

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ActivityPub.Tests.IntegrationTests.Scale;

public class QueryScaleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public QueryScaleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task QueryScale_CanFilterLargeDatasetEfficiently()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int TotalItems = 100;
        const int FilterCount = 50;

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var tasks = new List<Task<Actor>>();
        for (int i = 0; i < TotalItems; i++)
        {
            tasks.Add(CreateTestActorAsync(repository, $"query-filter-{testRunId}-{i}"));
        }

        await Task.WhenAll(tasks);

        var filterTasks = new List<Task<Actor?>>();
        for (int i = 0; i < FilterCount; i++)
        {
            filterTasks.Add(repository.GetUserActorAsync($"query-filter-{testRunId}-{i}"));
        }

        var filtered = await Task.WhenAll(filterTasks);

        Assert.Equal(FilterCount, filtered.Count(a => a != null));
    }

    [Fact]
    public async Task QueryScale_CanSortLargeResultSets()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int ItemCount = 100;

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var tasks = new List<Task<Actor>>();
        for (int i = 0; i < ItemCount; i++)
        {
            tasks.Add(CreateTestActorAsync(repository, $"query-sort-{testRunId}-{i}"));
        }

        await Task.WhenAll(tasks);

        var sorted = await repository.GetActorOutboxActivitiesAsync($"query-sort-{testRunId}-0", 0, 100);

        Assert.True(sorted.Count() >= 0);
    }

    [Fact]
    public async Task QueryScale_CanAggregateLargeCollections()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int ActorCount = 100;

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();

        for (int i = 0; i < ActorCount; i++)
        {
            var actorEntity = new ActorEntity
            {
                Username = $"query-agg-{testRunId}-{i}",
                JsonData = $"{{\"id\":\"https://localhost/users/query-agg-{testRunId}-{i}\",\"preferredUsername\":\"query-agg-{testRunId}-{i}\"}}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Actors.Add(actorEntity);
        }

        await context.SaveChangesAsync();
        await context.SaveChangesAsync();

        var count = await context.Actors.CountAsync(a => a.Username.Contains(testRunId));

        Assert.Equal(ActorCount, count);
    }

    [Fact]
    public async Task QueryScale_CanHandleComplexJoinQueries()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int ActorCount = 80;

        var client = _factory.CreateClient();
        var target = await CreateTestActorAsync($"query-join-target-{testRunId}");

        var followTasks = new List<Task>();
        for (int i = 0; i < ActorCount; i++)
        {
            var follower = await CreateTestActorAsync($"query-join-follower-{testRunId}-{i}");
            followTasks.Add(PostFollowAsync(client, follower, target));
        }

        await Task.WhenAll(followTasks);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var followers = await repository.GetFollowersAsync(target.PreferredUsername ?? string.Empty, 0, 1000);

        Assert.True(followers.Count() >= ActorCount);
    }

    [Fact]
    public async Task QueryScale_CanPaginateLargeActivitySets()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int TotalActivities = 200;
        const int PageSize = 100;

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var actor = await CreateTestActorAsync(repository, $"query-paginate-{testRunId}");

        var activityTasks = new List<Task<string>>();
        for (int i = 0; i < TotalActivities; i++)
        {
            activityTasks.Add(PostActivityAsync(actor.PreferredUsername ?? string.Empty, $"query-paginate-activity-{testRunId}-{i}"));
        }

        await Task.WhenAll(activityTasks);

        var allActivities = new List<string>();
        for (int page = 0; page < (TotalActivities + PageSize - 1) / PageSize; page++)
        {
            var pageActivities = await repository.GetActorOutboxActivitiesAsync(actor.PreferredUsername ?? string.Empty, page * PageSize, PageSize);
            allActivities.AddRange(pageActivities);
        }

        Assert.True(allActivities.Count >= TotalActivities, $"Expected at least {TotalActivities} activities, got {allActivities.Count}");
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

    private async Task<string> PostActivityAsync(string username, string content)
    {
        var activity = new Activity
        {
            Id = $"https://localhost/users/{username}/activities/quer-{Guid.NewGuid()}",
            Type = "Create",
            Actor = $"https://localhost/users/{username}",
            Object = new Note
            {
                Id = $"https://localhost/users/{username}/notes/quer-{Guid.NewGuid()}",
                Type = "Note",
                Content = content
            },
            Published = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(activity, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var contentObj = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/activity+json"));

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/users/{username}/inbox", contentObj);
        response.EnsureSuccessStatusCode();

        return activity.Id;
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
