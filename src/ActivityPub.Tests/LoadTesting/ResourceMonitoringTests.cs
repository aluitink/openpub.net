using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.LoadTesting;

public class ResourceMonitoringTests : LoadTestBase
{
    private const int ConcurrentUsers = 20;
    private const int Iterations = 300;

    public ResourceMonitoringTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<ResourceMonitoringResult> TestMemoryUsageUnderLoad(int concurrentUsers, int iterations)
    {
        var beforeMemory = GC.GetTotalMemory(true);
        var process = Process.GetCurrentProcess();
        var beforePrivateBytes = process.PrivateMemorySize64;
        var beforeWorkingSet = process.WorkingSet64;

        await RunLoadTestAsync(
            async () =>
            {
                var actor = await CreateTestActorAsync($"mem-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
                var activity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{actor.PreferredUsername}/activities/mem-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Create",
                    Actor = actor.Id,
                    Object = new global::ActivityPub.Core.Models.Note
                    {
                        Id = $"https://localhost/users/{actor.PreferredUsername}/notes/mem-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        Type = "Note",
                        Content = "Memory test activity with some content to ensure proper memory allocation and garbage collection testing"
                    }
                };

                var content = CreateActivityContent(activity);
                return await _client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
            },
            concurrentUsers,
            iterations);

        var afterMemory = GC.GetTotalMemory(false);
        process.Refresh();
        var afterPrivateBytes = process.PrivateMemorySize64;
        var afterWorkingSet = process.WorkingSet64;

        return new ResourceMonitoringResult
        {
            BeforeGCAllocatedBytes = beforeMemory,
            AfterGCAllocatedBytes = afterMemory,
            MemoryDelta = afterMemory - beforeMemory,
            BeforePrivateBytes = beforePrivateBytes,
            AfterPrivateBytes = afterPrivateBytes,
            PrivateBytesDelta = afterPrivateBytes - beforePrivateBytes,
            BeforeWorkingSet = beforeWorkingSet,
            AfterWorkingSet = afterWorkingSet,
            WorkingSetDelta = afterWorkingSet - beforeWorkingSet
        };
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<ResourceMonitoringResult> TestCpuUsageUnderLoad(int concurrentUsers, int iterations)
    {
        var process = Process.GetCurrentProcess();
        var beforeCpuTime = process.TotalProcessorTime;
        var beforeUserTime = process.UserProcessorTime;
        var beforePrivTime = process.PrivilegedProcessorTime;

        var startTime = DateTime.UtcNow;

        await RunLoadTestAsync(
            async () =>
            {
                var actor = await CreateTestActorAsync($"cpu-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
                var activity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{actor.PreferredUsername}/activities/cpu-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Create",
                    Actor = actor.Id,
                    Object = new global::ActivityPub.Core.Models.Note
                    {
                        Id = $"https://localhost/users/{actor.PreferredUsername}/notes/cpu-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        Type = "Note",
                        Content = "CPU test activity"
                    }
                };

                var content = CreateActivityContent(activity);
                return await _client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
            },
            concurrentUsers,
            iterations);

        var endTime = DateTime.UtcNow;
        process.Refresh();
        var afterCpuTime = process.TotalProcessorTime;
        var afterUserTime = process.UserProcessorTime;
        var afterPrivTime = process.PrivilegedProcessorTime;

        var durationSeconds = (endTime - startTime).TotalSeconds;

        return new ResourceMonitoringResult
        {
            DurationSeconds = durationSeconds,
            BeforeTotalCpuTime = beforeCpuTime,
            AfterTotalCpuTime = afterCpuTime,
            TotalCpuTimeUsed = afterCpuTime - beforeCpuTime,
            BeforeUserCpuTime = beforeUserTime,
            AfterUserCpuTime = afterUserTime,
            UserCpuTimeUsed = afterUserTime - beforeUserTime,
            BeforePrivCpuTime = beforePrivTime,
            AfterPrivCpuTime = afterPrivTime,
            PrivCpuTimeUsed = afterPrivTime - beforePrivTime,
            CpuUsagePercent = durationSeconds > 0 ? ((afterCpuTime - beforeCpuTime).TotalSeconds / durationSeconds) * 100 : 0
        };
    }

    [Benchmark]
    [Arguments(ConcurrentUsers, Iterations)]
    public async Task<ResourceMonitoringResult> TestMemoryAndCpuCombined(int concurrentUsers, int iterations)
    {
        var beforeMemory = GC.GetTotalMemory(true);
        var process = Process.GetCurrentProcess();
        var beforePrivateBytes = process.PrivateMemorySize64;
        var beforeWorkingSet = process.WorkingSet64;
        var beforeCpuTime = process.TotalProcessorTime;

        await RunLoadTestAsync(
            async () =>
            {
                var actor = await CreateTestActorAsync($"combined-user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
                var activity = new global::ActivityPub.Core.Models.Activity
                {
                    Id = $"https://localhost/users/{actor.PreferredUsername}/activities/combined-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type = "Create",
                    Actor = actor.Id,
                    Object = new global::ActivityPub.Core.Models.Note
                    {
                        Id = $"https://localhost/users/{actor.PreferredUsername}/notes/combined-bench-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        Type = "Note",
                        Content = "Combined memory and CPU test activity with longer content to ensure proper resource utilization"
                    }
                };

                var content = CreateActivityContent(activity);
                return await _client.PostAsync($"/users/{actor.PreferredUsername}/inbox", content);
            },
            concurrentUsers,
            iterations);

        process.Refresh();
        var afterMemory = GC.GetTotalMemory(false);
        var afterPrivateBytes = process.PrivateMemorySize64;
        var afterWorkingSet = process.WorkingSet64;
        var afterCpuTime = process.TotalProcessorTime;

        return new ResourceMonitoringResult
        {
            BeforeGCAllocatedBytes = beforeMemory,
            AfterGCAllocatedBytes = afterMemory,
            MemoryDelta = afterMemory - beforeMemory,
            BeforePrivateBytes = beforePrivateBytes,
            AfterPrivateBytes = afterPrivateBytes,
            PrivateBytesDelta = afterPrivateBytes - beforePrivateBytes,
            BeforeWorkingSet = beforeWorkingSet,
            AfterWorkingSet = afterWorkingSet,
            WorkingSetDelta = afterWorkingSet - beforeWorkingSet,
            BeforeTotalCpuTime = beforeCpuTime,
            AfterTotalCpuTime = afterCpuTime,
            TotalCpuTimeUsed = afterCpuTime - beforeCpuTime,
            CpuUsagePercent = ((afterCpuTime - beforeCpuTime).TotalSeconds / (iterations / (double)concurrentUsers)) * 100
        };
    }
}

public class ResourceMonitoringResult
{
    public long BeforeGCAllocatedBytes { get; set; }
    public long AfterGCAllocatedBytes { get; set; }
    public long MemoryDelta { get; set; }

    public long BeforePrivateBytes { get; set; }
    public long AfterPrivateBytes { get; set; }
    public long PrivateBytesDelta { get; set; }

    public long BeforeWorkingSet { get; set; }
    public long AfterWorkingSet { get; set; }
    public long WorkingSetDelta { get; set; }

    public TimeSpan BeforeTotalCpuTime { get; set; }
    public TimeSpan AfterTotalCpuTime { get; set; }
    public TimeSpan TotalCpuTimeUsed { get; set; }

    public TimeSpan BeforeUserCpuTime { get; set; }
    public TimeSpan AfterUserCpuTime { get; set; }
    public TimeSpan UserCpuTimeUsed { get; set; }

    public TimeSpan BeforePrivCpuTime { get; set; }
    public TimeSpan AfterPrivCpuTime { get; set; }
    public TimeSpan PrivCpuTimeUsed { get; set; }

    public double DurationSeconds { get; set; }
    public double CpuUsagePercent { get; set; }
}
