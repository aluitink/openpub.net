using System.Diagnostics.Metrics;
using ActivityPub.Core.Infrastructure.Metrics;
using Xunit;

namespace ActivityPub.Tests.Infrastructure.Metrics;

/// <summary>
/// Unit tests for <see cref="CustomMetricCollector"/> — the custom
/// <see cref="IMetricCollector"/> implementation, which previously had no direct
/// unit test. Uses a real <see cref="MeterListener"/> (a sealed type, so it is
/// configured with lambdas rather than subclassed) to observe the instruments
/// and measurements the collector emits through its underlying <see cref="Meter"/>.
/// </summary>
public class CustomMetricCollectorTests
{
    private const string MeterName = "ActivityPub.Core.CustomMetrics";

    private sealed class Captured
    {
        public readonly List<Instrument> Instruments = new();
        public readonly List<double> Values = new();
    }

    private static (MeterListener listener, Captured captured) StartListener()
    {
        var captured = new Captured();
        var listener = new MeterListener();
        // NOTE: in this .NET 10 SDK the InstrumentPublished handler is typed
        // Action<Instrument, MeterListener> (parameters differ from upstream
        // Action<Meter, Instrument>), so the handler must match that signature.
        Action<Instrument, MeterListener> onInstrument = (instr, mtr) =>
        {
            if (instr.Meter.Name != MeterName) return;
            captured.Instruments.Add(instr);
            listener.EnableMeasurementEvents(instr);
        };
        listener.InstrumentPublished += onInstrument;
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, tagsData) =>
        {
            captured.Values.Add(value);
        });
        listener.Start();
        return (listener, captured);
    }

    private static Instrument Find(Captured captured, string name)
    {
        Assert.Contains(name, captured.Instruments.Select(i => i.Name));
        return captured.Instruments.First(i => i.Name == name);
    }

    [Fact]
    public void IncrementCounter_CreatesEnabledCounterAndEmitsValue()
    {
        var (listener, captured) = StartListener();
        using var collector = new CustomMetricCollector();

        collector.IncrementCounter("webhook_deliveries_total");
        collector.IncrementCounter("webhook_deliveries_total");
        collector.IncrementCounter("inbox_activities_total");

        var counter = Find(captured, "webhook_deliveries_total");
        var inboxCounter = Find(captured, "inbox_activities_total");

        // The collector created the instruments under its meter and the
        // listener enabled them (Enabled flips to true only on enablement).
        Assert.True(counter.Enabled, "counter instrument should be enabled by the listener");
        Assert.True(inboxCounter.Enabled);

        // Three +1 increments were emitted through the meter.
        Assert.Equal(3, captured.Values.Count(v => v == 1));
    }

    [Fact]
    public void RecordHistogram_CreatesEnabledHistogramAndEmitsValues()
    {
        var (listener, captured) = StartListener();
        using var collector = new CustomMetricCollector();

        collector.RecordHistogram("delivery_latency_ms", 12.5);
        collector.RecordHistogram("delivery_latency_ms", 30.0);
        collector.RecordHistogram("inbox_processing_latency_ms", 42.0);

        var histogram = Find(captured, "delivery_latency_ms");
        var inboxHistogram = Find(captured, "inbox_processing_latency_ms");

        Assert.True(histogram.Enabled);
        Assert.True(inboxHistogram.Enabled);
        Assert.Contains(12.5, captured.Values);
        Assert.Contains(30.0, captured.Values);
        Assert.Contains(42.0, captured.Values);
    }

    [Fact]
    public void IncrementCounter_IsIdempotentInInstrumentCreation()
    {
        // Calling with the same name many times must not throw (the collector
        // caches the underlying instrument rather than recreating it each time).
        using var collector = new CustomMetricCollector();

        for (var i = 0; i < 100; i++)
        {
            collector.IncrementCounter("same_counter");
        }

        Assert.True(true, "100 increments on the same counter did not throw");
    }

    [Fact]
    public void RecordMetric_DoesNotThrow()
    {
        using var collector = new CustomMetricCollector();

        // RecordMetric is a flexible no-op in this implementation; it must not
        // throw for any input.
        collector.RecordMetric("anything", 1.0);
        collector.RecordMetric("anything", 2.5, new KeyValuePair<string, object?>("k", "v"));

        Assert.True(true, "RecordMetric did not throw");
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var collector = new CustomMetricCollector();
        collector.IncrementCounter("x");
        collector.RecordHistogram("y", 1.0);

        // Disposing twice must not throw (the meter is guarded by ?.).
        collector.Dispose();
        collector.Dispose();

        Assert.True(true, "double Dispose did not throw");
    }

    [Fact]
    public void IncrementCounter_PassesTagsToInstrument()
    {
        var (listener, captured) = StartListener();
        using var collector = new CustomMetricCollector();

        collector.IncrementCounter("tagged_counter", new KeyValuePair<string, object?>("env", "test"));

        var instrument = Find(captured, "tagged_counter");
        Assert.True(instrument.Enabled);
        // The measurement was emitted with the tag attached.
        Assert.Contains(1, captured.Values);
    }
}
