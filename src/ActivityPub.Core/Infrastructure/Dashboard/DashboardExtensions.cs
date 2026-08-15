using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ActivityPub.Core.Infrastructure.Dashboard;

/// <summary>
/// Extension methods for configuring dashboard functionality
/// </summary>
public static class DashboardExtensions
{
    /// <summary>
    /// Adds dashboard monitoring capabilities to the service collection
    /// </summary>
    public static IServiceCollection AddDashboardMonitoring(this IServiceCollection services)
    {
        // Add any dashboard-specific services here
        return services;
    }

    /// <summary>
    /// Configures the application to enable dashboard endpoints
    /// </summary>
    public static IApplicationBuilder UseDashboardEndpoints(this IApplicationBuilder app)
    {
        // Dashboard endpoints are automatically registered through controllers
        return app;
    }
}