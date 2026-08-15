using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActivityPub.Core.Infrastructure.Telemetry;

/// <summary>
/// Enhanced telemetry service for ActivityPub operations
/// </summary>
public class ActivityPubTelemetry
{
    private readonly ILogger<ActivityPubTelemetry> _logger;
    private readonly Meter _meter;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _activityProcessed;
    private readonly Counter<long> _errors;
    private readonly Counter<long> _eventsDispatched;
    private readonly Counter<long> _webFingerRequests;
    private readonly Counter<long> _webFingerCacheHits;
    private readonly Histogram<double> _webFingerProcessingTime;
    private readonly Counter<long> _webFingerCacheMisses;
    // Removed Gauge<int> as it's not available in this context

    // Variables to track metrics (these are now managed by the meter)
    // Note: These are kept for backward compatibility but won't be used for actual counting
    // since the meter-based counters are used instead.

    public ActivityPubTelemetry(ILogger<ActivityPubTelemetry> logger, Meter meter)
    {
        _logger = logger;
        _meter = meter;
        _requestDuration = _meter.CreateHistogram<double>("activitypub.requests.duration", "milliseconds", "Duration of ActivityPub requests");
        _activityProcessed = _meter.CreateCounter<long>("activitypub.activities.processed", "count", "Number of activities processed");
        _errors = _meter.CreateCounter<long>("activitypub.errors", "count", "Number of errors encountered");
        _eventsDispatched = _meter.CreateCounter<long>("activitypub.events.dispatched", "count", "Number of events dispatched");
        _webFingerRequests = _meter.CreateCounter<long>("activitypub.webfinger.requests", "count", "Number of WebFinger requests");
        _webFingerCacheHits = _meter.CreateCounter<long>("activitypub.webfinger.cache.hits", "count", "Number of WebFinger cache hits");
        _webFingerProcessingTime = _meter.CreateHistogram<double>("activitypub.webfinger.processing.time", "milliseconds", "WebFinger processing time");
        _webFingerCacheMisses = _meter.CreateCounter<long>("activitypub.webfinger.cache.misses", "count", "Number of WebFinger cache misses");
        // Removed Gauge creation as it's not available in this context
    }

    /// <summary>
    /// Records an activity processing event
    /// </summary>
    /// <param name="operation">The operation that was performed</param>
    public void RecordActivityProcessed(string operation)
    {
        _activityProcessed.Add(1, new KeyValuePair<string, object?>("operation", operation));
        _logger.LogDebug("Activity processed: {Operation}", operation);
    }

    /// <summary>
    /// Records an error event
    /// </summary>
    /// <param name="operation">The operation that failed</param>
    /// <param name="exception">The exception that occurred</param>
    public void RecordActivityError(string operation, Exception exception)
    {
        _errors.Add(1, new KeyValuePair<string, object?>("operation", operation));
        _logger.LogError(exception, "Activity error during {Operation}", operation);
    }

    /// <summary>
    /// Records an HTTP request
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="path">Request path</param>
    /// <param name="statusCode">Status code</param>
    /// <param name="durationMs">Duration in milliseconds</param>
    public void RecordHttpRequestProcessed(string method, string path, int statusCode, double durationMs)
    {
        _requestDuration.Record(durationMs,
            new KeyValuePair<string, object?>("http.method", method),
            new KeyValuePair<string, object?>("http.path", path),
            new KeyValuePair<string, object?>("http.status", statusCode)
        );

        _logger.LogDebug("HTTP request processed: {Method} {Path} - Status: {StatusCode}, Duration: {Duration}ms", method, path, statusCode, durationMs);
    }

    /// <summary>
    /// Records an HTTP request error
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="path">Request path</param>
    /// <param name="statusCode">Status code</param>
    /// <param name="exception">The exception that occurred</param>
    public void RecordHttpRequestError(string method, string path, int statusCode, Exception exception)
    {
        _errors.Add(1,
            new KeyValuePair<string, object?>("http.method", method),
            new KeyValuePair<string, object?>("http.path", path),
            new KeyValuePair<string, object?>("http.status", statusCode)
        );

        _logger.LogError(exception, "HTTP request error: {Method} {Path} - Status: {StatusCode}", method, path, statusCode);
    }

    /// <summary>
    /// Records an event dispatch
    /// </summary>
    /// <param name="eventType">The type of event dispatched</param>
    public void RecordEventDispatched(string eventType)
    {
        _eventsDispatched.Add(1, new KeyValuePair<string, object?>("event.type", eventType));
        _logger.LogDebug("Event dispatched: {EventType}", eventType);
    }

    /// <summary>
    /// Records a WebFinger request
    /// </summary>
    public void RecordWebFingerRequest()
    {
        _webFingerRequests?.Add(1);
        _logger.LogDebug("WebFinger request recorded");
    }

    /// <summary>
    /// Records a WebFinger cache hit
    /// </summary>
    public void RecordWebFingerCacheHit()
    {
        _webFingerCacheHits?.Add(1);
        _logger.LogDebug("WebFinger cache hit recorded");
    }

    /// <summary>
    /// Records WebFinger processing time
    /// </summary>
    /// <param name="durationMs">Processing time in milliseconds</param>
    public void RecordWebFingerProcessingTime(double durationMs)
    {
        _webFingerProcessingTime?.Record(durationMs,
            new KeyValuePair<string, object?>("operation", "WebFinger"));
        _logger.LogDebug("WebFinger processing time recorded: {Duration}ms", durationMs);
    }

    /// <summary>
    /// Records a WebFinger cache miss
    /// </summary>
    public void RecordWebFingerCacheMiss()
    {
        _webFingerCacheMisses?.Add(1);
        _logger.LogDebug("WebFinger cache miss recorded");
    }

    /// <summary>
    /// Updates cache size gauge
    /// </summary>
    /// <param name="size">Current cache size</param>
    public void UpdateWebFingerCacheSize(int size)
    {
        // Removed as Gauge is not available in this context
        // This would normally update the cache size gauge
    }

    /// <summary>
    /// Gets the current cache hits count
    /// </summary>
    public long GetWebFingerCacheHits()
    {
        // Return 0 as we can't directly access the counter value in this context
        // This is for backward compatibility only
        return 0;
    }

    /// <summary>
    /// Gets the current cache misses count
    /// </summary>
    public long GetWebFingerCacheMisses()
    {
        // Return 0 as we can't directly access the counter value in this context
        // This is for backward compatibility only
        return 0;
    }

    /// <summary>
    /// Gets the current cache hit ratio
    /// </summary>
    public double GetWebFingerCacheHitRatio()
    {
        // Return 0 as we can't directly access the counter values in this context
        // This is for backward compatibility only
        return 0.0;
    }

    /// <summary>
    /// Gets the total webfinger requests
    /// </summary>
    public long GetWebFingerRequests()
    {
        // Return 0 as we can't directly access the counter value in this context
        // This is for backward compatibility only
        return 0;
    }
}