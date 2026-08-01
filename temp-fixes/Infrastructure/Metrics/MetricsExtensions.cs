using Microsoft.Extensions.DependencyInjection;

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
        // Add any custom metrics configuration here if needed
        return services;
    }
}