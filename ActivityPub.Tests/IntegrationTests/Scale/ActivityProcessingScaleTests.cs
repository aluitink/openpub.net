using Xunit;
using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.IntegrationTests.Scale;

public class ActivityProcessingScaleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActivityProcessingScaleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ActivityProcessingScale_CanProcessManyActivities()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"actuser-{testRunId}";
        var actorId = $"https://localhost/users/{username}";
        var inboxUrl = $"https://localhost/users/{username}/inbox";

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var actor = new Actor
        {
            Id = actorId,
            Type = "Person",
            PreferredUsername = username,
            Inbox = inboxUrl
        };

        await repository.SaveUserActorAsync(actor);

        const int ActivityCount = 300;

        for (int i = 0; i < ActivityCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/process{i}";
            var noteId = $"https://localhost/users/{username}/notes/process{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Activity processing test {i}"
                }
            };

            await repository.SaveActivityAsync(activity);
        }

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var count = await context.Activities.CountAsync(a => a.ActivityId.Contains(testRunId));

        Assert.Equal(ActivityCount, count);
    }

    [Fact]
    public async Task ActivityProcessingScale_CanProcessMixedActivityTypes()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"actuser-{testRunId}";
        var actorId = $"https://localhost/users/{username}";
        var inboxUrl = $"https://localhost/users/{username}/inbox";

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var actor = new Actor
        {
            Id = actorId,
            Type = "Person",
            PreferredUsername = username,
            Inbox = inboxUrl
        };

        await repository.SaveUserActorAsync(actor);

        var activities = new List<Activity>();

        for (int i = 0; i < 60; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/create{i}";
            var noteId = $"https://localhost/users/{username}/notes/create{i}";

            activities.Add(new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Create {i}"
                }
            });
        }

        for (int i = 0; i < 40; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/like{i}";
            var noteId = $"https://localhost/users/{username}/notes/like{i}";

            activities.Add(new Activity
            {
                Id = activityId,
                Type = "Like",
                Actor = actorId,
                Object = noteId
            });
        }

        for (int i = 0; i < 30; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/announce{i}";
            var noteId = $"https://localhost/users/{username}/notes/announce{i}";

            activities.Add(new Activity
            {
                Id = activityId,
                Type = "Announce",
                Actor = actorId,
                Object = noteId
            });
        }

        foreach (var activity in activities)
        {
            await repository.SaveActivityAsync(activity);
        }

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var total = await context.Activities.CountAsync(a => a.ActivityId.Contains(testRunId));

        Assert.Equal(130, total);
    }

    [Fact]
    public async Task ActivityProcessingScale_CanProcessActivityWithNestedObjects()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"actuser-{testRunId}";
        var actorId = $"https://localhost/users/{username}";
        var inboxUrl = $"https://localhost/users/{username}/inbox";

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var actor = new Actor
        {
            Id = actorId,
            Type = "Person",
            PreferredUsername = username,
            Inbox = inboxUrl
        };

        await repository.SaveUserActorAsync(actor);

        const int ActivityCount = 100;

        for (int i = 0; i < ActivityCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/nested{i}";
            var noteId = $"https://localhost/users/{username}/notes/nested{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Nested object test {i}",
                    To = new[] { "https://www.w3.org/ns/activitypub#Public" }
                }
            };

            await repository.SaveActivityAsync(activity);
        }

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var count = await context.Activities.CountAsync(a => a.ActivityId.Contains(testRunId));

        Assert.Equal(ActivityCount, count);
    }

    [Fact]
    public async Task ActivityProcessingScale_CanProcessActivityWithMentions()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"actuser-{testRunId}";
        var actorId = $"https://localhost/users/{username}";
        var inboxUrl = $"https://localhost/users/{username}/inbox";
        var mentionId = $"https://localhost/users/mentionuser-{testRunId}";

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var actor = new Actor
        {
            Id = actorId,
            Type = "Person",
            PreferredUsername = username,
            Inbox = inboxUrl
        };

        await repository.SaveUserActorAsync(actor);

        const int ActivityCount = 80;

        for (int i = 0; i < ActivityCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/mention{i}";
            var noteId = $"https://localhost/users/{username}/notes/mention{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Mention test {i}",
                    Tag = new[] { mentionId }
                }
            };

            await repository.SaveActivityAsync(activity);
        }

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var count = await context.Activities.CountAsync(a => a.ActivityId.Contains(testRunId));

        Assert.Equal(ActivityCount, count);
    }
}
