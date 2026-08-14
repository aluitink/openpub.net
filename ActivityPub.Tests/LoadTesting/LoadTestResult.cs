using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ActivityPub.Tests.LoadTesting;

public class LoadTestResult
{
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double TotalDurationSeconds { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double MinResponseTimeMs { get; set; }
    public double MaxResponseTimeMs { get; set; }
    public double RequestsPerSecond { get; set; }
}
