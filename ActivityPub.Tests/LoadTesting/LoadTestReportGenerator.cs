using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Text.Json;

namespace ActivityPub.Tests.LoadTesting;

public static class LoadTestReportGenerator
{
    public static string GenerateReport(LoadTestResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Load Test Report ===");
        sb.AppendLine();
        sb.AppendLine($"Total Requests: {result.TotalRequests}");
        sb.AppendLine($"Successful Requests: {result.SuccessfulRequests}");
        sb.AppendLine($"Failed Requests: {result.FailedRequests}");
        sb.AppendLine($"Success Rate: {(result.TotalRequests > 0 ? (result.SuccessfulRequests / (double)result.TotalRequests * 100).ToString("F2") : "N/A")}%");
        sb.AppendLine();
        sb.AppendLine($"Total Duration: {result.TotalDurationSeconds:F2} seconds");
        sb.AppendLine($"Requests/Second: {result.RequestsPerSecond:F2}");
        sb.AppendLine();
        sb.AppendLine("Response Time Metrics:");
        sb.AppendLine($"  Average: {result.AverageResponseTimeMs:F2} ms");
        sb.AppendLine($"  Minimum: {result.MinResponseTimeMs:F2} ms");
        sb.AppendLine($"  Maximum: {result.MaxResponseTimeMs:F2} ms");
        sb.AppendLine();
        sb.AppendLine($"Test Time: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Test Framework: ActivityPub Load Testing Suite v1.0");
        return sb.ToString();
    }

    public static string GenerateResourceReport(ResourceMonitoringResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Resource Monitoring Report ===");
        sb.AppendLine();

        sb.AppendLine("Memory Usage:");
        sb.AppendLine($"  Before GC Allocated: {result.BeforeGCAllocatedBytes:N0} bytes");
        sb.AppendLine($"  After GC Allocated: {result.AfterGCAllocatedBytes:N0} bytes");
        sb.AppendLine($"  Memory Delta: {result.MemoryDelta:N0} bytes");
        sb.AppendLine($"  Before Private Bytes: {result.BeforePrivateBytes:N0} bytes");
        sb.AppendLine($"  After Private Bytes: {result.AfterPrivateBytes:N0} bytes");
        sb.AppendLine($"  Private Bytes Delta: {result.PrivateBytesDelta:N0} bytes");
        sb.AppendLine($"  Before Working Set: {result.BeforeWorkingSet:N0} bytes");
        sb.AppendLine($"  After Working Set: {result.AfterWorkingSet:N0} bytes");
        sb.AppendLine($"  Working Set Delta: {result.WorkingSetDelta:N0} bytes");
        sb.AppendLine();

        sb.AppendLine("CPU Usage:");
        sb.AppendLine($"  Duration: {result.DurationSeconds:F2} seconds");
        sb.AppendLine($"  Total CPU Time Used: {result.TotalCpuTimeUsed.TotalSeconds:F2} seconds");
        sb.AppendLine($"  User CPU Time Used: {result.UserCpuTimeUsed.TotalSeconds:F2} seconds");
        sb.AppendLine($"  Privileged CPU Time Used: {result.PrivCpuTimeUsed.TotalSeconds:F2} seconds");
        sb.AppendLine($"  CPU Usage: {result.CpuUsagePercent:F2}%");
        sb.AppendLine();

        sb.AppendLine($"Test Time: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Test Framework: ActivityPub Load Testing Suite v1.0");
        return sb.ToString();
    }

    public static string GenerateCombinedReport(
        LoadTestResult loadResult,
        ResourceMonitoringResult resourceResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Combined Load and Resource Test Report ===");
        sb.AppendLine();
        sb.AppendLine(GenerateReport(loadResult));
        sb.AppendLine();
        sb.AppendLine(GenerateResourceReport(resourceResult));
        return sb.ToString();
    }

    public static async Task SaveReportToFile(string report, string filename = null)
    {
        filename ??= $"loadtest-report-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.txt";
        var path = Path.Combine(Directory.GetCurrentDirectory(), filename);
        await File.WriteAllTextAsync(path, report);
    }

    public static string SerializeToJson(LoadTestResult result)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Serialize(result, options);
    }

    public static string SerializeToJson(ResourceMonitoringResult result)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Serialize(result, options);
    }
}
