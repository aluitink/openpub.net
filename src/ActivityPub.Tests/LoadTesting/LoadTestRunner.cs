using Microsoft.AspNetCore.Mvc.Testing;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.LoadTesting;

public static class LoadTestRunner
{
    private static readonly TestWebApplicationFactory _factory = new();
    private static readonly HttpClient _client = _factory.CreateClient();

    static LoadTestRunner()
    {
        _client.DefaultRequestHeaders.Accept.TryParseAdd("application/activity+json");
        _client.DefaultRequestHeaders.UserAgent.TryParseAdd("ActivityPub-LoadTest/1.0");
    }

    public static async Task RunAllTestsAsync()
    {
        Console.WriteLine("=== ActivityPub Load Testing Suite ===");
        Console.WriteLine($"Started at: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss zzz}");
        Console.WriteLine();

        var results = new List<LoadTestResult>();
        var resourceResults = new List<ResourceMonitoringResult>();

        await TestApiEndpointsAsync(results);
        await TestFederationEndpointsAsync(results);
        await TestMemoryAndCpuUsageAsync(resourceResults);

        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine();

        if (results.Count > 0)
        {
            var totalRequests = results.Sum(r => r.TotalRequests);
            var totalSuccess = results.Sum(r => r.SuccessfulRequests);
            var totalDuration = results.Sum(r => r.TotalDurationSeconds);
            var avgRps = results.Average(r => r.RequestsPerSecond);

            Console.WriteLine($"Total Requests: {totalRequests}");
            Console.WriteLine($"Total Successful: {totalSuccess}");
            Console.WriteLine($"Total Duration: {totalDuration:F2} seconds");
            Console.WriteLine($"Average Requests/Second: {avgRps:F2}");
            Console.WriteLine();

            var avgResponseTime = results.Average(r => r.AverageResponseTimeMs);
            var minResponseTime = results.Min(r => r.MinResponseTimeMs);
            var maxResponseTime = results.Max(r => r.MaxResponseTimeMs);

            Console.WriteLine($"Average Response Time: {avgResponseTime:F2} ms");
            Console.WriteLine($"Min Response Time: {minResponseTime:F2} ms");
            Console.WriteLine($"Max Response Time: {maxResponseTime:F2} ms");
            Console.WriteLine();
        }

        if (resourceResults.Count > 0)
        {
            var avgMemoryDelta = resourceResults.Average(r => r.MemoryDelta);
            var avgCpuUsage = resourceResults.Average(r => r.CpuUsagePercent);

            Console.WriteLine($"Average Memory Delta: {avgMemoryDelta:N0} bytes");
            Console.WriteLine($"Average CPU Usage: {avgCpuUsage:F2}%");
            Console.WriteLine();
        }

        Console.WriteLine($"Completed at: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss zzz}");
    }

    private static async Task TestApiEndpointsAsync(List<LoadTestResult> results)
    {
        Console.WriteLine("Testing API Endpoints...");
        Console.WriteLine();

        await TestCreateActivity(results);
        await TestDeliveryEndpoint(results);
        await TestRetrievalEndpoint(results);
        await TestActorLookup(results);
        await TestWebFingerEndpoint(results);
    }

    private static async Task TestFederationEndpointsAsync(List<LoadTestResult> results)
    {
        Console.WriteLine("Testing Federation Endpoints...");
        Console.WriteLine();

        await TestSharedInboxDelivery(results);
        await TestFollowerOperations(results);
        await TestActivityPropagation(results);
        await TestUndoOperations(results);
    }

    private static async Task TestMemoryAndCpuUsageAsync(List<ResourceMonitoringResult> results)
    {
        Console.WriteLine("Testing Memory and CPU Usage...");
        Console.WriteLine();

        var actor = await CreateTestActorAsync("resource-test");
        var beforeMemory = GC.GetTotalMemory(true);
        var process = Process.GetCurrentProcess();
        var beforePrivateBytes = process.PrivateMemorySize64;

        var activity = new global::ActivityPub.Core.Models.Activity
        {
            Id = $"https://localhost/users/{actor.PreferredUsername}/activities/resource-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Type = "Create",
            Actor = actor.Id,
            Object = new global::ActivityPub.Core.Models.Note
            {
                Id = $"https://localhost/users/{actor.PreferredUsername}/notes/resource-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Type = "Note",
                Content = "Resource monitoring test activity with content to ensure proper memory allocation"
            }
        };

        var content = CreateActivityContent(activity);
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 50; i++)
        {
            using var response = await _client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
        }

        sw.Stop();
        process.Refresh();

        results.Add(new ResourceMonitoringResult
        {
            BeforeGCAllocatedBytes = beforeMemory,
            AfterGCAllocatedBytes = GC.GetTotalMemory(false),
            MemoryDelta = GC.GetTotalMemory(false) - beforeMemory,
            BeforePrivateBytes = beforePrivateBytes,
            AfterPrivateBytes = process.PrivateMemorySize64,
            PrivateBytesDelta = process.PrivateMemorySize64 - beforePrivateBytes,
            DurationSeconds = sw.Elapsed.TotalSeconds,
            TotalCpuTimeUsed = process.TotalProcessorTime,
            CpuUsagePercent = sw.Elapsed.TotalSeconds > 0 ? (process.TotalProcessorTime.TotalSeconds / sw.Elapsed.TotalSeconds) * 100 : 0
        });
    }

    private static async Task TestCreateActivity(List<LoadTestResult> results)
    {
        var actor = await CreateTestActorAsync("perf-create");
        var activity = new global::ActivityPub.Core.Models.Activity
        {
            Id = $"https://localhost/users/{actor.PreferredUsername}/activities/test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Type = "Create",
            Actor = actor.Id,
            Object = new global::ActivityPub.Core.Models.Note
            {
                Id = $"https://localhost/users/{actor.PreferredUsername}/notes/test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Type = "Note",
                Content = "Performance test activity"
            }
        };

        var content = CreateActivityContent(activity);

        var result = await RunLoadTestAsync(
            async () => await _client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content),
            20, 100);

        results.Add(result);
        Console.WriteLine($"Create Activity: {result.AverageResponseTimeMs:F2}ms avg, {result.RequestsPerSecond:F2} rps");
    }

    private static async Task TestDeliveryEndpoint(List<LoadTestResult> results)
    {
        var actor = await CreateTestActorAsync("perf-delivery");

        var result = await RunLoadTestAsync(
            async () => await _client.GetAsync($"/users/{actor.PreferredUsername}/inbox"),
            20, 100);

        results.Add(result);
        Console.WriteLine($"Delivery: {result.AverageResponseTimeMs:F2}ms avg, {result.RequestsPerSecond:F2} rps");
    }

    private static async Task TestRetrievalEndpoint(List<LoadTestResult> results)
    {
        var actor = await CreateTestActorAsync("perf-retrieval");
        await CreateTestActivityAsync(actor.PreferredUsername ?? string.Empty);

        var result = await RunLoadTestAsync(
            async () => await _client.GetAsync($"/users/{actor.PreferredUsername}/outbox"),
            20, 100);

        results.Add(result);
        Console.WriteLine($"Retrieval: {result.AverageResponseTimeMs:F2}ms avg, {result.RequestsPerSecond:F2} rps");
    }

    private static async Task TestActorLookup(List<LoadTestResult> results)
    {
        var actor = await CreateTestActorAsync("perf-lookup");

        var result = await RunLoadTestAsync(
            async () => await _client.GetAsync($"/users/{actor.PreferredUsername}"),
            20, 100);

        results.Add(result);
        Console.WriteLine($"Actor Lookup: {result.AverageResponseTimeMs:F2}ms avg, {result.RequestsPerSecond:F2} rps");
    }

    private static async Task TestWebFingerEndpoint(List<LoadTestResult> results)
    {
        var result = await RunLoadTestAsync(
            async () => await _client.GetAsync("/.well-known/webfinger?resource=acct:test@localhost"),
            20, 100);

        results.Add(result);
        Console.WriteLine($"WebFinger: {result.AverageResponseTimeMs:F2}ms avg, {result.RequestsPerSecond:F2} rps");
    }

    private static async Task TestSharedInboxDelivery(List<LoadTestResult> results)
    {
        var actor = await CreateTestActorAsync("fed-shared");
        actor.SharedInbox = "https://localhost/inbox";

        var activity = new global::ActivityPub.Core.Models.Activity
        {
            Id = $"https://localhost/users/{actor.PreferredUsername}/activities/fed-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Type = "Create",
            Actor = actor.Id,
            Object = new global::ActivityPub.Core.Models.Note
            {
                Id = $"https://localhost/users/{actor.PreferredUsername}/notes/fed-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Type = "Note",
                Content = "Federation shared inbox test"
            },
            To = new List<string> { "https://localhost/users/followers" }
        };

        var content = CreateActivityContent(activity);

        var result = await RunLoadTestAsync(
            async () => await _client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content),
            10, 50);

        results.Add(result);
        Console.WriteLine($"Shared Inbox: {result.AverageResponseTimeMs:F2}ms avg, {result.RequestsPerSecond:F2} rps");
    }

    private static async Task TestFollowerOperations(List<LoadTestResult> results)
    {
        var followingActor = await CreateTestActorAsync("fed-following");

        var result = await RunLoadTestAsync(
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

                var content = CreateActivityContent(followActivity);
                return await _client.PostAsync($"/users/{followingActor.PreferredUsername}/inbox", content);
            },
            10, 50);

        results.Add(result);
        Console.WriteLine($"Follower Ops: {result.AverageResponseTimeMs:F2}ms avg, {result.RequestsPerSecond:F2} rps");
    }

    private static async Task TestActivityPropagation(List<LoadTestResult> results)
    {
        var author = await CreateTestActorAsync("fed-prop");

        var result = await RunLoadTestAsync(
            async () =>
            {
                var activity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{author.PreferredUsername}/activities/prop-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Create",
                    Actor = author.Id,
                    Object = new global::ActivityPub.Core.Models.Note
                    {
                        Id = $"https://localhost/users/{author.PreferredUsername}/notes/prop-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        Type = "Note",
                        Content = "Activity propagation test"
                    },
                    To = new List<string> { "https://localhost/users/followers" }
                };

                var content = CreateActivityContent(activity);
                return await _client.PostAsync($"/users/{author.PreferredUsername}/inbox", content);
            },
            10, 50);

        results.Add(result);
        Console.WriteLine($"Propagation: {result.AverageResponseTimeMs:F2}ms avg, {result.RequestsPerSecond:F2} rps");
    }

    private static async Task TestUndoOperations(List<LoadTestResult> results)
    {
        var user = await CreateTestActorAsync("fed-undo");

        var result = await RunLoadTestAsync(
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
            10, 50);

        results.Add(result);
        Console.WriteLine($"Undo Ops: {result.AverageResponseTimeMs:F2}ms avg, {result.RequestsPerSecond:F2} rps");
    }

    private static async Task<LoadTestResult> RunLoadTestAsync(
        Func<Task<HttpResponseMessage>> action,
        int concurrentUsers = 10,
        int iterations = 100)
    {
        var results = new List<double>();
        var successCount = 0;
        var failureCount = 0;
        var totalStartTime = DateTimeOffset.UtcNow;

        var semaphore = new SemaphoreSlim(concurrentUsers);

        var tasks = new List<Task>();

        for (int i = 0; i < iterations; i++)
        {
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    using var response = await action();
                    sw.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref successCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref failureCount);
                    }

                    lock (results)
                    {
                        results.Add(sw.ElapsedMilliseconds);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        var totalEndTime = DateTimeOffset.UtcNow;
        var totalDuration = totalEndTime - totalStartTime;

        var avgResponseTime = results.Count > 0 ? results.Average() : 0;
        var minResponseTime = results.Count > 0 ? results.Min() : 0;
        var maxResponseTime = results.Count > 0 ? results.Max() : 0;

        return new LoadTestResult
        {
            TotalRequests = iterations,
            SuccessfulRequests = successCount,
            FailedRequests = failureCount,
            TotalDurationSeconds = totalDuration.TotalSeconds,
            AverageResponseTimeMs = avgResponseTime,
            MinResponseTimeMs = minResponseTime,
            MaxResponseTimeMs = maxResponseTime,
            RequestsPerSecond = iterations / totalDuration.TotalSeconds
        };
    }

    private static async Task<string> SerializeActivityAsync(global::ActivityPub.Core.Models.Activity activity)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Serialize(activity, options);
    }

    private static StringContent CreateActivityContent(global::ActivityPub.Core.Models.Activity activity)
    {
        var json = SerializeActivityAsync(activity).Result;
        return new StringContent(json, Encoding.UTF8, "application/activity+json");
    }

    private static async Task<global::ActivityPub.Core.Models.Actor> CreateTestActorAsync(string username)
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

    private static async Task<global::ActivityPub.Core.Models.Activity> CreateTestActivityAsync(string username, string content = "Test activity")
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var activity = new global::ActivityPub.Core.Models.Activity
        {
            Id = $"https://localhost/users/{username}/activities/test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Type = "Create",
            Actor = $"https://localhost/users/{username}",
            Object = new global::ActivityPub.Core.Models.Note
            {
                Id = $"https://localhost/users/{username}/notes/test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Type = "Note",
                Content = content
            }
        };

        await repository.SaveActivityAsync(activity);
        return activity;
    }
}
