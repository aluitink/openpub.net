using Microsoft.AspNetCore.Mvc.Testing;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Tests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Diagnostics;

namespace ActivityPub.Tests.IntegrationTests.Performance;

public class PerformanceOptimizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PerformanceOptimizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DatabaseQuery_ResponseTimeUnder100ms()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var username = $"perfuser{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var actor = new ActivityPub.Core.Models.Actor
        {
            Id = $"https://localhost/users/perf-test-{Guid.NewGuid()}",
            Type = "Person",
            PreferredUsername = username,
            Inbox = $"https://localhost/users/perfuser/inbox",
            Outbox = $"https://localhost/users/perfuser/outbox"
        };

        await repository.SaveUserActorAsync(actor);

        var sw = Stopwatch.StartNew();
        var retrieved = await repository.GetUserActorAsync(username);
        sw.Stop();

        Assert.NotNull(retrieved);
        Assert.True(sw.ElapsedMilliseconds < 100, $"Query took {sw.ElapsedMilliseconds}ms, expected < 100ms");
    }

    [Fact]
    public async Task ActivitySave_ResponseTimeUnder50ms()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var username = $"actuser{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var actor = new ActivityPub.Core.Models.Actor
        {
            Id = $"https://localhost/users/act-test-{Guid.NewGuid()}",
            Type = "Person",
            PreferredUsername = username,
            Inbox = $"https://localhost/users/actuser/inbox",
            Outbox = $"https://localhost/users/actuser/outbox"
        };

        await repository.SaveUserActorAsync(actor);

        var activity = new ActivityPub.Core.Models.Activity
        {
            Id = $"https://localhost/users/actuser/activities/{Guid.NewGuid()}",
            Type = "Create",
            Actor = actor.Id,
            Object = new ActivityPub.Core.Models.Note
            {
                Id = $"https://localhost/users/actuser/notes/{Guid.NewGuid()}",
                Type = "Note",
                Content = "Performance test"
            }
        };

        var sw = Stopwatch.StartNew();
        await repository.SaveActivityAsync(activity);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 50, $"Save took {sw.ElapsedMilliseconds}ms, expected < 50ms");
    }

    [Fact]
    public async Task ActivityQuery_ResponseTimeUnder100ms()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var username = $"queryuser{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var actor = new ActivityPub.Core.Models.Actor
        {
            Id = $"https://localhost/users/query-test-{Guid.NewGuid()}",
            Type = "Person",
            PreferredUsername = username,
            Inbox = $"https://localhost/users/queryuser/inbox",
            Outbox = $"https://localhost/users/queryuser/outbox"
        };

        await repository.SaveUserActorAsync(actor);

        var activityId = $"https://localhost/users/queryuser/activities/{Guid.NewGuid()}";
        var activity = new ActivityPub.Core.Models.Activity
        {
            Id = activityId,
            Type = "Create",
            Actor = actor.Id,
            Object = new ActivityPub.Core.Models.Note
            {
                Id = $"https://localhost/users/queryuser/notes/{Guid.NewGuid()}",
                Type = "Note",
                Content = "Query performance test"
            }
        };

        await repository.SaveActivityAsync(activity);

        var sw = Stopwatch.StartNew();
        var results = await repository.GetActorOutboxActivitiesAsync(username, 0, 10);
        sw.Stop();

        Assert.True(results.Any());
        Assert.True(sw.ElapsedMilliseconds < 100, $"Query took {sw.ElapsedMilliseconds}ms, expected < 100ms");
    }

    [Fact]
    public async Task BulkInsert_100ActivitiesUnder1000ms()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var username = $"bulkuser{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var actor = new ActivityPub.Core.Models.Actor
        {
            Id = $"https://localhost/users/bulk-test-{Guid.NewGuid()}",
            Type = "Person",
            PreferredUsername = username,
            Inbox = $"https://localhost/users/bulkuser/inbox",
            Outbox = $"https://localhost/users/bulkuser/outbox"
        };

        await repository.SaveUserActorAsync(actor);

        var activities = new List<ActivityPub.Core.Models.Activity>();
        for (int i = 0; i < 100; i++)
        {
            activities.Add(new ActivityPub.Core.Models.Activity
            {
                Id = $"https://localhost/users/bulkuser/activities/bulk-{i}-{Guid.NewGuid()}",
                Type = "Create",
                Actor = actor.Id,
                Object = new ActivityPub.Core.Models.Note
                {
                    Id = $"https://localhost/users/bulkuser/notes/bulk-{i}-{Guid.NewGuid()}",
                    Type = "Note",
                    Content = $"Bulk test activity {i}"
                }
            });
        }

        var sw = Stopwatch.StartNew();
        foreach (var activity in activities)
        {
            await repository.SaveActivityAsync(activity);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000, $"100 activities took {sw.ElapsedMilliseconds}ms, expected < 1000ms");
    }
}
