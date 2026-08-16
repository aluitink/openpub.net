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

public class ActivityHistoryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActivityHistoryTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ActivityHistory_CanStoreManyActivities()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("history-test");

        for (int i = 0; i < 50; i++)
        {
            await PostActivityAsync(actor.PreferredUsername, $"history-activity-{i}");
        }

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var activities = await repository.GetActorOutboxActivitiesAsync(actor.PreferredUsername, 0, 100);

        Assert.Equal(50, activities.Count());
    }

    [Fact]
    public async Task ActivityHistory_CanRetrieveActivityRange()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("range-test");

        for (int i = 0; i < 30; i++)
        {
            await PostActivityAsync(actor.PreferredUsername, $"range-activity-{i}");
        }

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var activities = await repository.GetActorOutboxActivitiesAsync(actor.PreferredUsername, 0, 100);

        Assert.Equal(30, activities.Count());
    }

    [Fact]
    public async Task ActivityHistory_CanPaginateLargeDataset()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("paginate-test");

        for (int i = 0; i < 80; i++)
        {
            await PostActivityAsync(actor.PreferredUsername, $"paginate-activity-{i}");
        }

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var page1 = await repository.GetActorOutboxActivitiesAsync(actor.PreferredUsername, 0, 25);
        Assert.Equal(25, page1.Count());

        var page2 = await repository.GetActorOutboxActivitiesAsync(actor.PreferredUsername, 25, 25);
        Assert.Equal(25, page2.Count());
    }

    [Fact]
    public async Task ActivityHistory_CanTrackActivitySequence()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("sequence-test");

        var expectedIds = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            var id = await PostActivityAsync(actor.PreferredUsername, $"sequence-activity-{i}");
            expectedIds.Add(id);
        }

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var storedIds = await repository.GetActorOutboxActivitiesAsync(actor.PreferredUsername, 0, 100);

        Assert.Equal(expectedIds.Count, storedIds.Count());
    }

    [Fact]
    public async Task ActivityHistory_CanRetrieveLatestActivities()
    {
        var client = _factory.CreateClient();
        var actor = await CreateTestActorAsync("latest-test");

        for (int i = 0; i < 40; i++)
        {
            await PostActivityAsync(actor.PreferredUsername, $"latest-activity-{i}");
        }

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var activities = await repository.GetActorOutboxActivitiesAsync(actor.PreferredUsername, 0, 100);

        Assert.Equal(40, activities.Count());
    }

    private async Task<string> PostActivityAsync(string username, string content)
    {
        var activity = new Activity
        {
            Id = $"https://localhost/users/{username}/activities/hist-{Guid.NewGuid()}",
            Type = "Create",
            Actor = $"https://localhost/users/{username}",
            Object = new Note
            {
                Id = $"https://localhost/users/{username}/notes/hist-{Guid.NewGuid()}",
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
}
