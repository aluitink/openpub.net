using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace ActivityPub.Core.Infrastructure.Metrics;

/// <summary>
/// Extension methods for ActivityPub metrics
/// </summary>
public static class MetricsExtensions
{
    /// <summary>
    /// Adds custom metrics services
    /// </summary>
    public static IServiceCollection AddCustomMetrics(this IServiceCollection services)
    {
        // Configure metrics collection
        services.AddMetrics();
        return services;
    }
}