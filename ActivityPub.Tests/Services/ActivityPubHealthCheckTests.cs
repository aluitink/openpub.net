using ActivityPub.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ActivityPub.Tests.Services;

public class ActivityPubHealthCheckTests
{
    private readonly ActivityPubHealthCheck _healthCheck;

    public ActivityPubHealthCheckTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<ActivityPubHealthCheck>();
        _healthCheck = new ActivityPubHealthCheck(logger);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy()
    {
        var context = new HealthCheckContext();

        var result = await _healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
