using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Core.Infrastructure.Caching;

/// <summary>
/// Extension methods for configuring response caching across ActivityPub endpoints
/// </summary>
public static class ResponseCachingExtensions
{
    /// <summary>
    /// Adds response caching support to the service collection
    /// </summary>
    public static IServiceCollection AddResponseCaching(this IServiceCollection services)
    {
        services.AddResponseCaching();
        return services;
    }

    /// <summary>
    /// Configures response caching middleware for the application
    /// </summary>
    public static IApplicationBuilder UseResponseCaching(this IApplicationBuilder app)
    {
        app.UseResponseCaching();
        return app;
    }
}
