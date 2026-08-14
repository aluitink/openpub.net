using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Diagnostics;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.LoadTesting;

public class FederationLoadTests : LoadTestBase
{
    private const int ConcurrentUsers = 10;
    private const int Iterations = 100;

    public FederationLoadTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestSharedInboxDelivery(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"shared-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        actor.SharedInbox = "https://localhost/inbox";

        return await RunLoadTestAsync(
            async () =>
            {
                var activity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{actor.PreferredUsername}/activities/fed-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Create",
                    Actor = actor.Id,
                    Object = new global::ActivityPub.Core.Models.Note
                    {
                        Id = $"https://localhost/users/{actor.PreferredUsername}/notes/fed-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        Type = "Note",
                        Content = "Federation shared inbox test"
                    },
                    To = new List<string> { "https://localhost/users/followers" }
                };

                var content = CreateActivityContent(activity);
                return await _client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestFollowerOperations(int concurrentUsers, int iterations)
    {
        var followingActor = await CreateTestActorAsync($"follow-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

        return await RunLoadTestAsync(
            async () =>
            {
                var followerActor = await CreateTestActorAsync($"follower-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

                var followActivity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{followerActor.PreferredUsername}/activities/follow-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Follow",
                    Actor = followerActor.Id,
                    Object = followingActor.Id
                };

                var content = CreateActivityContent(followActivity);
                return await _client.PostAsync($"/users/{followingActor.PreferredUsername}/inbox", content);
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestActivityPropagation(int concurrentUsers, int iterations)
    {
        var author = await CreateTestActorAsync($"prop-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

        return await RunLoadTestAsync(
            async () =>
            {
                var activity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{author.PreferredUsername}/activities/prop-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Create",
                    Actor = author.Id,
                    Object = new global::ActivityPub.Core.Models.Note
                    {
                        Id = $"https://localhost/users/{author.PreferredUsername}/notes/prop-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        Type = "Note",
                        Content = "Activity propagation test"
                    },
                    To = new List<string> { "https://localhost/users/followers" }
                };

                var content = CreateActivityContent(activity);
                return await _client.PostAsync($"/users/{author.PreferredUsername}/inbox", content);
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestUndoOperations(int concurrentUsers, int iterations)
    {
        var user = await CreateTestActorAsync($"undo-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

        return await RunLoadTestAsync(
            async () =>
            {
                var originalFollow = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{user.PreferredUsername}/activities/original-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Follow",
                    Actor = user.Id,
                    Object = "https://localhost/users/other"
                };

                using var scope = _factory.Services.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
                await repository.SaveActivityAsync(originalFollow);

                var undoActivity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{user.PreferredUsername}/activities/undo-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Undo",
                    Actor = user.Id,
                    Object = originalFollow
                };

                var content = CreateActivityContent(undoActivity);
                return await _client.PostAsync($"/users/{user.PreferredUsername}/inbox", content);
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestAcceptRejectOperations(int concurrentUsers, int iterations)
    {
        var followingActor = await CreateTestActorAsync($"accept-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

        return await RunLoadTestAsync(
            async () =>
            {
                var follower = await CreateTestActorAsync($"follower-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

                var followActivity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{follower.PreferredUsername}/activities/follow-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Follow",
                    Actor = follower.Id,
                    Object = followingActor.Id
                };

                using var scope = _factory.Services.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
                await repository.SaveActivityAsync(followActivity);

                var acceptActivity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{followingActor.PreferredUsername}/activities/accept-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Accept",
                    Actor = followingActor.Id,
                    Object = followActivity
                };

                var content = CreateActivityContent(acceptActivity);
                return await _client.PostAsync($"/users/{followingActor.PreferredUsername}/inbox", content);
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestDeleteOperations(int concurrentUsers, int iterations)
    {
        var user = await CreateTestActorAsync($"delete-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        await CreateTestActivityAsync(user.PreferredUsername, "To be deleted");

        return await RunLoadTestAsync(
            async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

                var activities = await repository.GetActorOutboxActivitiesAsync(user.PreferredUsername, 0, 1);
                if (activities.Count > 0)
                {
                    var activityToDelete = activities.First();

                    var deleteActivity = new global::ActivityPub.Core.Models.Activity
                    {
                        Id = $"https://localhost/users/{user.PreferredUsername}/activities/delete-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        Type = "Delete",
                        Actor = user.Id,
                        Object = activityToDelete
                    };

                    var content = CreateActivityContent(deleteActivity);
                    return await _client.PostAsync($"/users/{user.PreferredUsername}/inbox", content);
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
            },
            concurrentUsers,
            iterations);
    }
}
