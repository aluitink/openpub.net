using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ActivityPub.Core.Infrastructure.Telemetry;

/// <summary>
/// Extension methods for configuring telemetry in ActivityPub
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Adds ActivityPub telemetry services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddActivityPubTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<ActivityPubTelemetry>();
        return services;
    }
}