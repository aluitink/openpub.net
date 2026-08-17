using ActivityPub.Core.BackgroundServices;
using ActivityPub.Core.Caching;
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
        return AddActivityPub(services, configureOptions, configureDbContext: null);
    }

    /// <summary>
    /// Adds ActivityPub services to the DI container with custom DbContext configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure ActivityPub options</param>
    /// <param name="configureDbContext">Action to configure the ActivityPub DbContext. If null, uses InMemory.</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddActivityPub(
        this IServiceCollection services,
        Action<ActivityPubOptions>? configureOptions,
        Action<DbContextOptionsBuilder>? configureDbContext)
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

        if (configureDbContext != null)
        {
            services.AddDbContext<ActivityPubDbContext>(configureDbContext);
        }
        else
        {
            services.AddDbContext<ActivityPubDbContext>(options =>
                options.UseInMemoryDatabase("ActivityPubDb"));
        }

        services.AddScoped<IActivityPubRepository, EFCoreActivityPubRepository>();
        services.AddScoped<IApplicationRepository, EFCoreApplicationRepository>();

        // API (Mastodon REST) rate limiting. The limiter holds shared
        // in-memory per-client window state, so it must be a singleton; the
        // options are read through IOptions so appsettings.json can tune it.
        services.AddSingleton<Core.Services.ApiRateLimiter>();
        services.AddScoped<ActivityPubEventDispatcher>();
        services.AddScoped<IKeyFetchingService, KeyFetchingService>();
        services.AddScoped<IKeyGenerationService, KeyGenerationService>();
        services.AddScoped<IFederationDiscoveryService, FederationDiscoveryService>();
        services.AddScoped<IOutboundSigningService, OutboundSigningService>();
        services.AddScoped<IOutboundActivityService, OutboundActivityService>();
        services.AddScoped<InboxProcessorService>();
        services.AddScoped<ActivityHandlerFactory>();
        services.AddScoped<WebFingerCacheService>();
        services.AddScoped<IActivityValidationService, ActivityValidationService>();
        services.AddScoped<ActivityPubService>();
        services.AddScoped<ISharedInboxService, SharedInboxService>();
        services.AddScoped<IFederationCache, MemoryFederationCache>();
        services.AddScoped<CacheInvalidationService>();
        services.AddScoped<Core.Services.IMRFService, Core.Services.MRFService>();
        services.AddScoped<IFederationHealthService, FederationHealthService>();
        services.AddScoped<IDiscoveryService, DiscoveryServiceImpl>();
        services.AddScoped<ICommunityService, CommunityServiceImpl>();
        services.AddScoped<InboxProcessor>();
        services.AddScoped<IPeerHealthService, PeerHealthService>();
        services.AddHostedService<SharedInboxBackgroundService>();
        services.AddHostedService<PeerHealthBackgroundService>();
        services.AddHostedService<InboxDeadLetterBackgroundService>();

        return services;
    }
}
