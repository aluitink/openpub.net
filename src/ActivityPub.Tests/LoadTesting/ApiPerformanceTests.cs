using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.LoadTesting;

public class ApiPerformanceTests : LoadTestBase
{
    private const int ConcurrentUsers = 20;
    private const int Iterations = 500;

    public ApiPerformanceTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Benchmark]
    [Arguments(10, 50)]
    public async Task<LoadTestResult> TestCreateActivityEndpoint(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"perf-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

        return await RunLoadTestAsync(
            async () =>
            {
                var activity = new Activity
                {
                    Id = $"https://localhost/users/{actor.PreferredUsername}/activities/bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Create",
                    Actor = actor.Id,
                    Object = new Note
                    {
                        Id = $"https://localhost/users/{actor.PreferredUsername}/notes/bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        Type = "Note",
                        Content = "Performance test activity"
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
    public async Task<LoadTestResult> TestDeliveryEndpoint(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"delivery-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

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
    public async Task<LoadTestResult> TestRetrievalEndpoint(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"retrieval-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        await CreateTestActivityAsync(actor.PreferredUsername ?? string.Empty, "Retrieval test");

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
    public async Task<LoadTestResult> TestActorLookupEndpoint(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"lookup-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

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
    public async Task<LoadTestResult> TestWebFingerEndpoint(int concurrentUsers, int iterations)
    {
        return await RunLoadTestAsync(
            async () =>
            {
                var response = await _client.GetAsync("/.well-known/webfinger?resource=acct:test@localhost");
                return response;
            },
            concurrentUsers,
            iterations);
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<LoadTestResult> TestActivityPubDocumentEndpoint(int concurrentUsers, int iterations)
    {
        var actor = await CreateTestActorAsync($"ap-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

        return await RunLoadTestAsync(
            async () =>
            {
                var response = await _client.GetAsync($"/users/{actor.PreferredUsername}.json");
                return response;
            },
            concurrentUsers,
            iterations);
    }
}
