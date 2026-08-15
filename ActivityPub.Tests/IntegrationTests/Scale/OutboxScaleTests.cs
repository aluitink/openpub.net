using Xunit;
using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.IntegrationTests.Scale;

public class OutboxScaleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public OutboxScaleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OutboxScale_CanStoreManyOutboxActivities()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"outboxuser-{testRunId}";
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

        const int ActivityCount = 250;

        for (int i = 0; i < ActivityCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/outbox/act{i}";
            var noteId = $"https://localhost/users/{username}/outbox/note{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Outbox test {i}"
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
    public async Task OutboxScale_CanQueryActivitiesByDateRange()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"outboxuser-{testRunId}";
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

        const int ActivityCount = 150;

        for (int i = 0; i < ActivityCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/outbox/act{i}";
            var noteId = $"https://localhost/users/{username}/outbox/note{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Outbox date range test {i}"
                }
            };

            await repository.SaveActivityAsync(activity);
        }

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();

        var activities = await context.Activities
            .Where(a => a.ActivityId.Contains(testRunId))
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .ToListAsync();

        Assert.Equal(50, activities.Count);
    }

    [Fact]
    public async Task OutboxScale_CanFilterActivitiesByType()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"outboxuser-{testRunId}";
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

        for (int i = 0; i < 60; i++)
        {
            var activityId = $"https://localhost/users/{username}/outbox/create{i}";
            var noteId = $"https://localhost/users/{username}/outbox/note{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Create activity {i}"
                }
            };

            await repository.SaveActivityAsync(activity);
        }

        for (int i = 0; i < 40; i++)
        {
            var activityId = $"https://localhost/users/{username}/outbox/like{i}";
            var noteId = $"https://localhost/users/{username}/outbox/like{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Like",
                Actor = actorId,
                Object = noteId
            };

            await repository.SaveActivityAsync(activity);
        }

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();

        var createCount = await context.Activities.CountAsync(a => 
            a.ActivityId.Contains(testRunId) && a.JsonData.Contains("\"type\":\"Create\""));

        var likeCount = await context.Activities.CountAsync(a => 
            a.ActivityId.Contains(testRunId) && a.JsonData.Contains("\"type\":\"Like\""));

        Assert.Equal(60, createCount);
        Assert.Equal(40, likeCount);
    }

    [Fact]
    public async Task OutboxScale_CanHandleConcurrentActivityWrites()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"outboxuser-{testRunId}";
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

        var tasks = new List<Task>();

        for (int i = 0; i < ActivityCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/outbox/concurrent{i}";
            var noteId = $"https://localhost/users/{username}/outbox/note{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Outbox concurrent test {i}"
                }
            };

            tasks.Add(repository.SaveActivityAsync(activity));
        }

        await Task.WhenAll(tasks);

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();

        var count = await context.Activities.CountAsync(a => a.ActivityId.Contains(testRunId));

        Assert.Equal(ActivityCount, count);
    }

    [Fact]
    public async Task OutboxScale_CanPaginateResultSets()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"outboxuser-{testRunId}";
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

        const int ActivityCount = 200;

        for (int i = 0; i < ActivityCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/outbox/act{i}";
            var noteId = $"https://localhost/users/{username}/outbox/note{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Outbox pagination test {i}"
                }
            };

            await repository.SaveActivityAsync(activity);
        }

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();

        const int PageSize = 50;
        var items = await context.Activities
            .Where(a => a.ActivityId.Contains(testRunId))
            .OrderBy(a => a.Id)
            .Skip(75)
            .Take(PageSize)
            .ToListAsync();

        Assert.Equal(PageSize, items.Count);
    }
}
