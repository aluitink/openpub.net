using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.LoadTesting;

public class ResponseTimeTests : LoadTestBase
{
    private const int ConcurrentUsers = 15;
    private const int Iterations = 200;

    public ResponseTimeTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestCreateActivityResponseTime(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"res-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

        return await RunLoadTestAsync(
            async () =>
            {
                var activity = new Activity
                {
                    Id = $"https://localhost/users/{actor.PreferredUsername}/activities/res-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Create",
                    Actor = actor.Id,
                    Object = new Note
                    {
                        Id = $"https://localhost/users/{actor.PreferredUsername}/notes/res-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        Type = "Note",
                        Content = "Response time test activity"
                    }
                };

                var content = CreateActivityContent(activity);
                return await _client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestActivityDeliveryResponseTime(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"del-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        var activity = await CreateTestActivityAsync(actor.PreferredUsername, "Delivery test");

        return await RunLoadTestAsync(
            async () =>
            {
                var response = await _client.GetAsync($"/users/{actor.PreferredUsername}/inbox");
                return response;
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestActivityRetrievalResponseTime(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"ret-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        await CreateTestActivityAsync(actor.PreferredUsername, "Retrieval test");

        return await RunLoadTestAsync(
            async () =>
            {
                var response = await _client.GetAsync($"/users/{actor.PreferredUsername}/outbox");
                return response;
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestActorProfileResponseTime(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"prof-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

        return await RunLoadTestAsync(
            async () =>
            {
                var response = await _client.GetAsync($"/users/{actor.PreferredUsername}");
                return response;
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestWebFingerResolutionResponseTime(int concurrentUsers, int iterations)
    {
        return await RunLoadTestAsync(
            async () =>
            {
                var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:perf@localhost");
                return response;
            },
            concurrentUsers,
            iterations);
    }
}
