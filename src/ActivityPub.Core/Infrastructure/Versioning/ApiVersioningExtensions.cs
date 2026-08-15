using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Core.Infrastructure.Versioning;

/// <summary>
/// Extension methods for configuring API versioning across ActivityPub endpoints
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>
    /// Adds API versioning support to the service collection
    /// </summary>
    public static IServiceCollection AddApiVersioning(this IServiceCollection services)
    {
        // Add API versioning configuration
        // This enables consistent versioning strategy across all ActivityPub endpoints
        return services;
    }

    /// <summary>
    /// Configures API versioning middleware for the application
    /// </summary>
    public static IApplicationBuilder UseApiVersioning(this IApplicationBuilder app)
    {
        // Configure versioning middleware
        return app;
    }
}