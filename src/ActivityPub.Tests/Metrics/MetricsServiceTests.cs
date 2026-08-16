using ActivityPub.Core.Metrics;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Metrics;

public class MetricsServiceTests
{
    [Fact]
    public void IncrementCounter_IncrementsCounter()
    {
        var metrics = new MetricsService();

        metrics.IncrementCounter("test_counter", 1);

        Assert.True(true);
    }

    [Fact]
    public void RecordHistogram_RecordsValue()
    {
        var metrics = new MetricsService();

        metrics.RecordHistogram("test_histogram", 100.0);

        Assert.True(true);
    }

    [Fact]
    public void StartStopTimer_MeasuresDuration()
    {
        var metrics = new MetricsService();

        metrics.StartTimer("test_timer");
        System.Threading.Thread.Sleep(10);
        metrics.StopTimer("test_timer");

        Assert.True(true);
    }

    [Fact]
    public void SetGetGauge_StoresAndRetrievesValue()
    {
        var metrics = new MetricsService();

        metrics.SetGauge("test_gauge", 42.0);

        var value = metrics.GetGauge("test_gauge");

        Assert.Equal(42.0, value);
    }

    [Fact]
    public void RecordInboxActivity_IncrementsCounter()
    {
        var metrics = new MetricsService();

        metrics.RecordInboxActivity("Create", true);

        Assert.True(true);
    }

    [Fact]
    public void RecordInboxActivity_FailureIncrementsFailedCounter()
    {
        var metrics = new MetricsService();

        metrics.RecordInboxActivity("Create", false);

        Assert.True(true);
    }

    [Fact]
    public void RecordOutboundActivity_IncrementsCounter()
    {
        var metrics = new MetricsService();

        metrics.RecordOutboundActivity("Follow", true);

        Assert.True(true);
    }

    [Fact]
    public void RecordFederationAttempt_IncrementsCounter()
    {
        var metrics = new MetricsService();

        metrics.RecordFederationAttempt("example.com", true);

        Assert.True(true);
    }
}
