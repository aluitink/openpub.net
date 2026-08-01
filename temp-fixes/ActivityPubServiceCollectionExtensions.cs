using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;

namespace ActivityPub.Core;

/// <summary>
/// Extension methods for ActivityPub services
/// </summary>
public static class ActivityPubServiceCollectionExtensions
{
    /// <summary>
    /// Adds ActivityPub services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The updated service collection</returns>
    public static IServiceCollection AddActivityPub(this IServiceCollection services)
    {
        // Add core services
        services.AddActivityPubMonitoring();
        
        // Add repositories
        services.AddSingleton<IActivityPubRepository, InMemoryActivityPubRepository>();
        
        // Add event handling
        services.AddSingleton<ActivityPubEventDispatcher>();
        
        // Add services
        services.AddSingleton<ActivityPubService>();
        services.AddSingleton<InboxProcessorService>();
        
        return services;
    }
}