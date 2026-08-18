using System.Diagnostics.Metrics;
using ActivityPub.Core.Controllers.Dashboard;
using ActivityPub.Core.Infrastructure.Metrics;
using ActivityPub.Core.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="OperationalInsightsController"/> — the dashboard
/// controller for operational insights/monitoring, which previously had no
/// direct unit test. Drives the controller with a real
/// <see cref="ActivityPubTelemetry"/> (over a <see cref="Meter"/>) and a
/// mocked <see cref="IMetricCollector"/> and asserts on the returned
/// <see cref="OkObjectResult"/> payload shape (via JSON serialization, since
/// the payload is an anonymous type).
/// </summary>
public class OperationalInsightsControllerTests
{
    private static OperationalInsightsController Build()
    {
        var telemetry = new ActivityPubTelemetry(
            NullLogger<ActivityPubTelemetry>.Instance,
            new Meter("test-activitypub"));
        var metricCollector = new Mock<IMetricCollector>();
        return new OperationalInsightsController(metricCollector.Object, telemetry);
    }

    private static JsonElement ToJson(object value) =>
        JsonSerializer.SerializeToElement(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    [Fact]
    public void Constructor_NullMetricCollector_Throws()
    {
        var telemetry = new ActivityPubTelemetry(NullLogger<ActivityPubTelemetry>.Instance, new Meter("test"));
        Assert.Throws<ArgumentNullException>(() => new OperationalInsightsController(null!, telemetry));
    }

    [Fact]
    public void Constructor_NullTelemetry_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OperationalInsightsController(new Mock<IMetricCollector>().Object, null!));
    }

    [Fact]
    public void GetMetrics_ReturnsWebFingerAndSystemMetrics()
    {
        var controller = Build();

        var ok = Assert.IsAssignableFrom<OkObjectResult>(controller.GetMetrics());
        var root = ToJson(ok.Value!);

        Assert.Equal("ActivityPub.Core", root.GetProperty("serviceName").GetString());
        Assert.Equal("1.0.0", root.GetProperty("version").GetString());

        var wf = root.GetProperty("webFingerMetrics");
        Assert.Equal(0, wf.GetProperty("totalRequests").GetInt32());
        Assert.Equal(0, wf.GetProperty("cacheHits").GetInt32());
        Assert.Equal(0, wf.GetProperty("cacheMisses").GetInt32());
        Assert.Equal(0.0, wf.GetProperty("cacheHitRatio").GetDouble());

        Assert.True(root.GetProperty("systemMetrics").TryGetProperty("uptimeSeconds", out _));
        Assert.True(root.GetProperty("systemMetrics").TryGetProperty("memoryUsageBytes", out _));
    }

    [Fact]
    public void GetDashboard_ReturnsServiceStatusAndHealthChecks()
    {
        var controller = Build();

        var ok = Assert.IsAssignableFrom<OkObjectResult>(controller.GetDashboard());
        var root = ToJson(ok.Value!);

        Assert.Equal("Operational", root.GetProperty("serviceStatus").GetString());
        Assert.Equal("ActivityPub.Core", root.GetProperty("serviceName").GetString());

        var health = root.GetProperty("healthCheck");
        Assert.Equal("Healthy", health.GetProperty("status").GetString());

        var checks = health.GetProperty("checks");
        Assert.Equal(4, checks.GetArrayLength());
        var names = checks.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToHashSet();
        Assert.Contains("DatabaseConnection", names);
        Assert.Contains("CacheService", names);
        Assert.Contains("WebFingerEndpoint", names);
        Assert.Contains("ActivityProcessing", names);
    }

    [Fact]
    public void GetDashboard_RecentActivity_ContainsExpectedTypes()
    {
        var controller = Build();

        var ok = Assert.IsAssignableFrom<OkObjectResult>(controller.GetDashboard());
        var root = ToJson(ok.Value!);

        var lastHour = root.GetProperty("recentActivity").GetProperty("lastHour");
        Assert.Equal(3, lastHour.GetArrayLength());
        var types = lastHour.EnumerateArray().Select(r => r.GetProperty("type").GetString()).ToHashSet();
        Assert.Contains("WebFinger", types);
        Assert.Contains("Activities", types);
        Assert.Contains("CacheHits", types);
    }
}
