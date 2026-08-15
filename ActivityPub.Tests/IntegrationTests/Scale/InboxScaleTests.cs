using Xunit;
using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.IntegrationTests.Scale;

public class InboxScaleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public InboxScaleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InboxScale_CanProcessManyInboxItems()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"inboxuser-{testRunId}";
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

        const int ItemCount = 200;

        for (int i = 0; i < ItemCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/test{i}";
            var noteId = $"https://localhost/users/{username}/notes/test{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Inbox scale test {i}"
                }
            };

            await repository.SaveActivityAsync(activity);
        }

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var count = await context.Activities.CountAsync(a => a.ActivityId.Contains(testRunId));

        Assert.Equal(ItemCount, count);
    }

    [Fact]
    public async Task InboxScale_CanPaginateInboxItems()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"inboxuser-{testRunId}";
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

        const int ItemCount = 150;

        for (int i = 0; i < ItemCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/test{i}";
            var noteId = $"https://localhost/users/{username}/notes/test{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Inbox pagination test {i}"
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
            .Skip(50)
            .Take(PageSize)
            .ToListAsync();

        Assert.Equal(PageSize, items.Count);
    }

    [Fact]
    public async Task InboxScale_CanFilterInboxByType()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"inboxuser-{testRunId}";
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

        for (int i = 0; i < 50; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/create{i}";
            var noteId = $"https://localhost/users/{username}/notes/create{i}";

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

        for (int i = 0; i < 30; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/like{i}";
            var noteId = $"https://localhost/users/{username}/notes/like{i}";

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

        Assert.Equal(50, createCount);
        Assert.Equal(30, likeCount);
    }

    [Fact]
    public async Task InboxScale_CanHandleConcurrentInboxWrites()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"inboxuser-{testRunId}";
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

        const int ItemCount = 100;

        var tasks = new List<Task>();

        for (int i = 0; i < ItemCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/concurrent{i}";
            var noteId = $"https://localhost/users/{username}/notes/concurrent{i}";

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = $"Concurrent inbox test {i}"
                }
            };

            tasks.Add(repository.SaveActivityAsync(activity));
        }

        await Task.WhenAll(tasks);

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var count = await context.Activities.CountAsync(a => a.ActivityId.Contains(testRunId));

        Assert.Equal(ItemCount, count);
    }

    [Fact]
    public async Task InboxScale_CanProcessInboxWithLargePayloads()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        var username = $"inboxuser-{testRunId}";
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

        const int ItemCount = 50;

        for (int i = 0; i < ItemCount; i++)
        {
            var activityId = $"https://localhost/users/{username}/activities/large{i}";
            var noteId = $"https://localhost/users/{username}/notes/large{i}";

            var largeContent = new string('x', 10000);

            var activity = new Activity
            {
                Id = activityId,
                Type = "Create",
                Actor = actorId,
                Object = new Note
                {
                    Id = noteId,
                    Type = "Note",
                    Content = largeContent
                }
            };

            await repository.SaveActivityAsync(activity);
        }

        using var dbScope = _factory.Services.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
        var count = await context.Activities.CountAsync(a => a.ActivityId.Contains(testRunId));

        Assert.Equal(ItemCount, count);
    }
}
