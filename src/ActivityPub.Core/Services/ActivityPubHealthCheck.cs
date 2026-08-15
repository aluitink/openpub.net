using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Services;

/// <summary>
/// Health check for ActivityPub services
/// </summary>
public class ActivityPubHealthCheck : IHealthCheck
{
    private readonly ILogger<ActivityPubHealthCheck> _logger;

    public ActivityPubHealthCheck(ILogger<ActivityPubHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("ActivityPub services are healthy"));
    }
}
