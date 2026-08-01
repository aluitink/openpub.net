using System.Diagnostics.Metrics;

namespace ActivityPub.Core.Infrastructure.Metrics;

/// <summary>
/// Interface for collecting custom metrics
/// </summary>
public interface IMetricCollector
{
    /// <summary>
    /// Records a metric with the given name and value
    /// </summary>
    void RecordMetric(string name, double value, params KeyValuePair<string, object?>[] tags);
    
    /// <summary>
    /// Records a counter increment
    /// </summary>
    void IncrementCounter(string name, params KeyValuePair<string, object?>[] tags);
    
    /// <summary>
    /// Records a histogram measurement
    /// </summary>
    void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags);
}