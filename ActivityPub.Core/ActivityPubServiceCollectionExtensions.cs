using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using ActivityPub.Core.Infrastructure.Telemetry;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Events;
using ActivityPub.Core.Services;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Middleware;

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
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddLogging();
        
        // Add IHttpContextAccessor which is required by OutboundSigningService
        services.AddHttpContextAccessor();
        
        // Add Meter which is required by ActivityPubTelemetry
        services.AddSingleton<Meter>(sp => new Meter("ActivityPub"));
        
        services.AddSingleton<IActivityPubRepository, InMemoryActivityPubRepository>();
        services.AddSingleton<ActivityPub.Core.Services.ActivityPubEventDispatcher>();
        
        services.AddSingleton<IKeyFetchingService, KeyFetchingService>();
        services.AddSingleton<OutboundSigningService>();
        // HttpSignatureMiddleware is registered but not used in the pipeline
        // It's kept for potential future use
        services.AddSingleton<ActivityPubService>();
        services.AddSingleton<InboxProcessorService>();
        
        services.AddSingleton<ActivityPubTelemetry>();
        services.AddSingleton<WebFingerCacheService>();
        services.AddSingleton<IWebFingerSource, DefaultWebFingerSource>();
        
        return services;
    }
}