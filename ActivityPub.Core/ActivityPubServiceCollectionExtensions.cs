using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using ActivityPub.Core.Infrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;

namespace ActivityPub.Core;

/// <summary>
/// Extension methods for adding ActivityPub to DI container
/// </summary>
public static class ActivityPubServiceCollectionExtensions
{
    /// <summary>
    /// Adds ActivityPub services to the DI container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure ActivityPub options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddActivityPub(
        this IServiceCollection services,
        Action<ActivityPubOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<ActivityPubOptions>(options => { });
        }

        services.AddHttpClient();
        services.AddMemoryCache();
        services.AddDbContext<ActivityPubDbContext>(options => 
            options.UseInMemoryDatabase("ActivityPubDb"));
        services.AddSingleton<Meter>(sp => new Meter("ActivityPub"));
        services.AddScoped<IActivityPubRepository, EFCoreActivityPubRepository>();
        services.AddScoped<ActivityPubEventDispatcher>();
        services.AddScoped<ActivityPubTelemetry>();
        services.AddScoped<InboxProcessorService>();
        services.AddScoped<ActivityHandlerFactory>();
        services.AddScoped<WebFingerCacheService>();

        return services;
    }
}
