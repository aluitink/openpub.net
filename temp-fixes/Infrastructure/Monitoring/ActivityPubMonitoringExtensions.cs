using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ActivityPub.Core.Infrastructure.Telemetry;
using System.Diagnostics.Metrics;

namespace ActivityPub.Core.Infrastructure.Monitoring;

/// <summary>
/// Enhanced monitoring and telemetry provider for ActivityPub operations
/// </summary>
public static class ActivityPubMonitoringExtensions
{
    private static readonly Meter _meter = new("ActivityPub.Core", "1.0.0");
    private static readonly Histogram<double> _requestDuration = _meter.CreateHistogram<double>("activitypub.requests.duration", "milliseconds", "Duration of ActivityPub requests");
    private static readonly Counter<long> _activityProcessed = _meter.CreateCounter<long>("activitypub.activities.processed", "count", "Number of activities processed");
    private static readonly Counter<long> _errors = _meter.CreateCounter<long>("activitypub.errors", "count", "Number of errors encountered");
    private static readonly Counter<long> _eventsDispatched = _meter.CreateCounter<long>("activitypub.events.dispatched", "count", "Number of events dispatched");

    /// <summary>
    /// Adds ActivityPub monitoring services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The updated service collection</returns>
    public static IServiceCollection AddActivityPubMonitoring(this IServiceCollection services)
    {
        // Add monitoring services
        services.AddSingleton<ActivityPubTelemetry>();
        
        // Register metrics for observability
        services.AddSingleton(_meter);
        
        return services;
    }
}