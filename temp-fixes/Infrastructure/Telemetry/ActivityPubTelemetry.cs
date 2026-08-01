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

    public ActivityPubTelemetry(ILogger<ActivityPubTelemetry> logger, Meter meter)
    {
        _logger = logger;
        _meter = meter;
        _requestDuration = _meter.CreateHistogram<double>("activitypub.requests.duration", "milliseconds", "Duration of ActivityPub requests");
        _activityProcessed = _meter.CreateCounter<long>("activitypub.activities.processed", "count", "Number of activities processed");
        _errors = _meter.CreateCounter<long>("activitypub.errors", "count", "Number of errors encountered");
        _eventsDispatched = _meter.CreateCounter<long>("activitypub.events.dispatched", "count", "Number of events dispatched");
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
}