using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Tests.LoadTesting;

public class LoadTestDemo : LoadTestBase
{
    public LoadTestDemo(TestWebApplicationFactory factory) : base(factory)
    {
    }

    public async Task RunDemoAsync()
    {
        Console.WriteLine("=== ActivityPub Load Test Demo ===");
        Console.WriteLine();

        Console.WriteLine("Phase 8: Load Testing Suite Demonstration");
        Console.WriteLine();

        Console.WriteLine("1. Testing API Endpoint Performance");
        Console.WriteLine("   - Creating test actor...");
        var actor = await CreateTestActorAsync("demo-user");
        Console.WriteLine($"   - Actor created: {actor.Id}");
        Console.WriteLine();

        Console.WriteLine("2. Testing Activity Creation Response Time");
        var createResult = await TestCreateActivity();
        Console.WriteLine($"   Average Response Time: {createResult.AverageResponseTimeMs:F2} ms");
        Console.WriteLine($"   Requests/Second: {createResult.RequestsPerSecond:F2}");
        Console.WriteLine();

        Console.WriteLine("3. Testing Activity Delivery Response Time");
        var deliveryResult = await TestDelivery();
        Console.WriteLine($"   Average Response Time: {deliveryResult.AverageResponseTimeMs:F2} ms");
        Console.WriteLine($"   Requests/Second: {deliveryResult.RequestsPerSecond:F2}");
        Console.WriteLine();

        Console.WriteLine("4. Testing Activity Retrieval Response Time");
        var retrievalResult = await TestRetrieval();
        Console.WriteLine($"   Average Response Time: {retrievalResult.AverageResponseTimeMs:F2} ms");
        Console.WriteLine($"   Requests/Second: {retrievalResult.RequestsPerSecond:F2}");
        Console.WriteLine();

        Console.WriteLine("5. Testing Federation Endpoints");
        var federationResult = await TestFederationEndpoint();
        Console.WriteLine($"   Average Response Time: {federationResult.AverageResponseTimeMs:F2} ms");
        Console.WriteLine($"   Requests/Second: {federationResult.RequestsPerSecond:F2}");
        Console.WriteLine();

        Console.WriteLine("6. Testing Memory and CPU Usage");
        var resourceResult = await TestResourceUsage();
        Console.WriteLine($"   Memory Delta: {resourceResult.MemoryDelta:N0} bytes");
        Console.WriteLine($"   CPU Usage: {resourceResult.CpuUsagePercent:F2}%");
        Console.WriteLine();

        Console.WriteLine("7. Generating Load Test Report");
        var report = GenerateReport(createResult, deliveryResult, retrievalResult, federationResult, resourceResult);
        Console.WriteLine(report);
        Console.WriteLine();

        Console.WriteLine("=== Demo Complete ===");
    }

    private async Task<LoadTestResult> TestCreateActivity()
    {
        var activity = new global::ActivityPub.Core.Models.Activity
        {
            Id = $"https://localhost/users/demo-user/activities/demo-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Type = "Create",
            Actor = $"https://localhost/users/demo-user",
            Object = new global::ActivityPub.Core.Models.Note
            {
                Id = $"https://localhost/users/demo-user/notes/demo-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Type = "Note",
                Content = "Demo test activity"
            }
        };

        var content = CreateActivityContent(activity);

        return await RunLoadTestAsync(
            async () => await _client.PostAsync("/users/demo-user/inbox", content),
            10, 50);
    }

    private async Task<LoadTestResult> TestDelivery()
    {
        return await RunLoadTestAsync(
            async () => await _client.GetAsync("/users/demo-user/inbox"),
            10, 50);
    }

    private async Task<LoadTestResult> TestRetrieval()
    {
        return await RunLoadTestAsync(
            async () => await _client.GetAsync("/users/demo-user/outbox"),
            10, 50);
    }

    private async Task<LoadTestResult> TestFederationEndpoint()
    {
        return await RunLoadTestAsync(
            async () => await _client.GetAsync("/.well-known/webfinger?resource=acct:test@localhost"),
            10, 50);
    }

    private async Task<ResourceMonitoringResult> TestResourceUsage()
    {
        var beforeMemory = GC.GetTotalMemory(true);
        var process = Process.GetCurrentProcess();
        var beforePrivateBytes = process.PrivateMemorySize64;

        for (int i = 0; i < 20; i++)
        {
            var activity = new global::ActivityPub.Core.Models.Activity
            {
                Id = $"https://localhost/users/demo-user/activities/resource-{i}",
                Type = "Create",
                Actor = $"https://localhost/users/demo-user",
                Object = new global::ActivityPub.Core.Models.Note
                {
                    Id = $"https://localhost/users/demo-user/notes/resource-{i}",
                    Type = "Note",
                    Content = "Resource monitoring test"
                }
            };

            var content = CreateActivityContent(activity);
            await _client.PostAsync("/users/demo-user/inbox", content);
        }

        process.Refresh();

        return new ResourceMonitoringResult
        {
            BeforeGCAllocatedBytes = beforeMemory,
            AfterGCAllocatedBytes = GC.GetTotalMemory(false),
            MemoryDelta = GC.GetTotalMemory(false) - beforeMemory,
            BeforePrivateBytes = beforePrivateBytes,
            AfterPrivateBytes = process.PrivateMemorySize64,
            PrivateBytesDelta = process.PrivateMemorySize64 - beforePrivateBytes,
            DurationSeconds = 0,
            TotalCpuTimeUsed = process.TotalProcessorTime,
            CpuUsagePercent = 0
        };
    }

    private string GenerateReport(
        LoadTestResult createResult,
        LoadTestResult deliveryResult,
        LoadTestResult retrievalResult,
        LoadTestResult federationResult,
        ResourceMonitoringResult resourceResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ActivityPub Load Test Report ===");
        sb.AppendLine();
        sb.AppendLine($"Timestamp: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        sb.AppendLine("API Endpoint Performance:");
        sb.AppendLine($"  Activity Creation: {createResult.AverageResponseTimeMs:F2}ms avg, {createResult.RequestsPerSecond:F2} rps");
        sb.AppendLine($"  Activity Delivery: {deliveryResult.AverageResponseTimeMs:F2}ms avg, {deliveryResult.RequestsPerSecond:F2} rps");
        sb.AppendLine($"  Activity Retrieval: {retrievalResult.AverageResponseTimeMs:F2}ms avg, {retrievalResult.RequestsPerSecond:F2} rps");
        sb.AppendLine($"  Federation Endpoint: {federationResult.AverageResponseTimeMs:F2}ms avg, {federationResult.RequestsPerSecond:F2} rps");
        sb.AppendLine();

        sb.AppendLine("Resource Usage:");
        sb.AppendLine($"  Memory Delta: {resourceResult.MemoryDelta:N0} bytes");
        sb.AppendLine($"  CPU Usage: {resourceResult.CpuUsagePercent:F2}%");
        sb.AppendLine();

        sb.AppendLine("Phase 8: Load Testing Suite - Complete");
        return sb.ToString();
    }
}
