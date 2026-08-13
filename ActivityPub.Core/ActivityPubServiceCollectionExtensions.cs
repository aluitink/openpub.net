using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        services.AddLogging();
        services.AddDbContext<ActivityPubDbContext>(options => 
            options.UseInMemoryDatabase("ActivityPubDb"));
        services.AddScoped<IActivityPubRepository, EFCoreActivityPubRepository>();
        services.AddScoped<ActivityPubEventDispatcher>();
        services.AddScoped<IKeyFetchingService, KeyFetchingService>();
        services.AddScoped<InboxProcessorService>();
        services.AddScoped<ActivityHandlerFactory>();
        services.AddScoped<WebFingerCacheService>();

        return services;
    }
}
