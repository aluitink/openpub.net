using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace ActivityPub.Core.Metrics;

public interface IMetricsService
{
    void IncrementCounter(string name, int value = 1);
    void RecordHistogram(string name, double value);
    void StartTimer(string name);
    void StopTimer(string name);
    void SetGauge(string name, double value);
}

public class MetricsService : IMetricsService
{
    private readonly ConcurrentDictionary<string, Counter<double>> _counters;
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms;
    private readonly ConcurrentDictionary<string, Stopwatch> _timers;
    private readonly ConcurrentDictionary<string, double> _gauges;
    private readonly Meter _meter;

    public MetricsService()
    {
        _counters = new ConcurrentDictionary<string, Counter<double>>();
        _histograms = new ConcurrentDictionary<string, Histogram<double>>();
        _timers = new ConcurrentDictionary<string, Stopwatch>();
        _gauges = new ConcurrentDictionary<string, double>();
        _meter = new Meter("ActivityPub.Core", "1.0.0");
    }

    public void IncrementCounter(string name, int value = 1)
    {
        if (string.IsNullOrEmpty(name)) return;

        var counter = _counters.GetOrAdd(name, n => _meter.CreateCounter<double>($"activitypub_{n}"));
        counter.Add(value);
    }

    public void RecordHistogram(string name, double value)
    {
        if (string.IsNullOrEmpty(name)) return;

        var histogram = _histograms.GetOrAdd(name, n => _meter.CreateHistogram<double>($"activitypub_{n}_duration"));
        histogram.Record(value);
    }

    public void StartTimer(string name)
    {
        if (string.IsNullOrEmpty(name)) return;

        _timers.AddOrUpdate(name, _ => Stopwatch.StartNew(), (_, _) => Stopwatch.StartNew());
    }

    public void StopTimer(string name)
    {
        if (string.IsNullOrEmpty(name)) return;

        if (_timers.TryRemove(name, out var stopwatch))
        {
            stopwatch.Stop();
            RecordHistogram($"{name}_duration", stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    public void SetGauge(string name, double value)
    {
        if (string.IsNullOrEmpty(name)) return;

        _gauges.AddOrUpdate(name, value, (_, _) => value);
    }

    public double GetGauge(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;

        return _gauges.TryGetValue(name, out var value) ? value : 0;
    }

    public void RecordInboxActivity(string activityType, bool success)
    {
        IncrementCounter($"inbox_{activityType?.ToLowerInvariant()}_count");

        if (!success)
        {
            IncrementCounter($"inbox_failed_count");
        }
    }

    public void RecordOutboundActivity(string targetType, bool success)
    {
        IncrementCounter($"outbound_{targetType?.ToLowerInvariant()}_count");

        if (!success)
        {
            IncrementCounter($"outbound_failed_count");
        }
    }

    public void RecordFederationAttempt(string domain, bool success)
    {
        IncrementCounter($"federation_{domain.Split('.')[0]}_count");

        if (!success)
        {
            IncrementCounter($"federation_failed_count");
        }
    }
}
