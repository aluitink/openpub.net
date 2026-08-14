using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace ActivityPub.Tests.LoadTesting;

public class LoadTestBase
{
    protected readonly TestWebApplicationFactory _factory;
    protected readonly HttpClient _client;

    protected LoadTestBase(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Accept.TryParseAdd("application/activity+json");
        _client.DefaultRequestHeaders.UserAgent.TryParseAdd("ActivityPub-LoadTest/1.0");
    }

    protected async Task<string> SerializeActivityAsync(global::ActivityPub.Core.Models.Activity activity)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Serialize(activity, options);
    }

    protected StringContent CreateActivityContent(global::ActivityPub.Core.Models.Activity activity)
    {
        var json = SerializeActivityAsync(activity).Result;
        return new StringContent(json, Encoding.UTF8, "application/activity+json");
    }

    protected async Task<global::ActivityPub.Core.Models.Actor> CreateTestActorAsync(string username)
    {
        var actor = new global::ActivityPub.Core.Models.Actor
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

    protected async Task<global::ActivityPub.Core.Models.Activity> CreateTestActivityAsync(string username, string content = "Test activity")
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

    protected async Task<double> MeasureResponseTimeAsync(Func<Task<HttpResponseMessage>> action)
    {
        var sw = Stopwatch.StartNew();
        using var response = await action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    protected async Task<LoadTestResult> RunLoadTestAsync(
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
}
