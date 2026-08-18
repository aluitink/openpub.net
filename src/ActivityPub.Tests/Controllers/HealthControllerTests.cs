using ActivityPub.Core.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="HealthController"/> — the liveness/health
/// endpoints, which previously had no direct unit test. Drives the controller
/// and asserts on the returned <see cref="OkObjectResult"/> payload shape
/// (via JSON serialization, since the payload is an anonymous type).
/// </summary>
public class HealthControllerTests
{
    private static HealthController Build() =>
        new(NullLogger<HealthController>.Instance);

    private static JsonElement ToJson(object value) =>
        JsonSerializer.SerializeToElement(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    [Fact]
    public void GetHealth_ReturnsHealthyStatusAndOperationalServices()
    {
        var controller = Build();

        var ok = Assert.IsAssignableFrom<OkObjectResult>(controller.GetHealth());
        var root = ToJson(ok.Value!);

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("timestamp", out _), "timestamp should be present");

        var services = root.GetProperty("services");
        Assert.Equal("operational", services.GetProperty("inbox").GetString());
        Assert.Equal("operational", services.GetProperty("federation").GetString());
        Assert.Equal("operational", services.GetProperty("storage").GetString());
    }

    [Fact]
    public void GetHealthDetails_ReturnsPerServiceChecksAndSummary()
    {
        var controller = Build();

        var ok = Assert.IsAssignableFrom<OkObjectResult>(controller.GetHealthDetails());
        var root = ToJson(ok.Value!);

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal("All ActivityPub services operational", root.GetProperty("summary").GetString());

        var services = root.GetProperty("services");

        Assert.Equal("operational", services.GetProperty("inbox").GetProperty("status").GetString());
        var inboxChecks = services.GetProperty("inbox").GetProperty("checks").EnumerateArray().Select(c => c.GetString()).ToHashSet();
        Assert.Equal(new[] { "database", "queue" }, inboxChecks);

        var federationChecks = services.GetProperty("federation").GetProperty("checks").EnumerateArray().Select(c => c.GetString()).ToHashSet();
        Assert.Equal(new[] { "dns", "http" }, federationChecks);

        var storageChecks = services.GetProperty("storage").GetProperty("checks").EnumerateArray().Select(c => c.GetString()).ToHashSet();
        Assert.Equal(new[] { "disk", "connections" }, storageChecks);
    }
}
