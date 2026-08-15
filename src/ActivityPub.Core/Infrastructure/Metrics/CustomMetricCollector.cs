using System.Diagnostics.Metrics;

namespace ActivityPub.Core.Infrastructure.Metrics;

/// <summary>
/// Custom implementation of metric collection with extensibility
/// </summary>
public class CustomMetricCollector : IMetricCollector, IDisposable
{
    private readonly Meter _meter;
    private readonly Dictionary<string, Counter<double>> _counters = new();
    private readonly Dictionary<string, Histogram<double>> _histograms = new();

    public CustomMetricCollector()
    {
        _meter = new Meter("ActivityPub.Core.CustomMetrics");
    }

    public void RecordMetric(string name, double value, params KeyValuePair<string, object?>[] tags)
    {
        // This allows for flexible metric recording
        // In a real implementation, you might want to normalize names
        // or handle different metric types based on naming conventions
    }

    public void IncrementCounter(string name, params KeyValuePair<string, object?>[] tags)
    {
        if (!_counters.TryGetValue(name, out var counter))
        {
            counter = _meter.CreateCounter<double>(name);
            _counters[name] = counter;
        }

        counter.Add(1, tags);
    }

    public void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags)
    {
        if (!_histograms.TryGetValue(name, out var histogram))
        {
            histogram = _meter.CreateHistogram<double>(name);
            _histograms[name] = histogram;
        }

        histogram.Record(value, tags);
    }

    public void Dispose()
    {
        _meter?.Dispose();
        _counters.Clear();
        _histograms.Clear();
    }
}