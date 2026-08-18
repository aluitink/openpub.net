using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FederationHealthService"/> — the federation
/// health/observability surface. These cover the previously-untested pure
/// logic: delivery-queue stats (all five status buckets + the error-rate
/// formula incl. the divide-by-zero guard), the recent-errors projection
/// (filter / ordering / limit / "Unknown" reason), and the
/// <c>DetermineOverallStatus</c> Healthy/Degraded/Critical/Warning thresholds
/// via <see cref="FederationHealthService.GetHealthStatusAsync"/>.
/// </summary>
public class FederationHealthServiceTests
{
    private static FederationHealthService CreateService(IActivityPubRepository repository)
    {
        var options = new DbContextOptionsBuilder<ActivityPubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new ActivityPubDbContext(options);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new NoopHandler()));
        return new FederationHealthService(
            repository,
            context,
            NullLogger<FederationHealthService>.Instance,
            factoryMock.Object);
    }

    private static SharedInboxDeliveryEntity Delivery(
        DeliveryStatus status,
        DateTime created,
        DateTime? lastAttempt = null,
        DateTime? updated = null,
        int retryCount = 0,
        string? failureReason = null)
    {
        return new SharedInboxDeliveryEntity
        {
            Id = Guid.NewGuid().ToString(),
            ActivityId = "https://remote.example/notes/1",
            ActivityJson = "{}",
            TargetActorId = "https://remote.example/users/bob",
            Status = status,
            RetryCount = retryCount,
            LastDeliveryAttempt = lastAttempt,
            FailureReason = failureReason,
            CreatedAt = created,
            UpdatedAt = updated ?? created
        };
    }

    // --- GetDeliveryQueueStatsAsync --------------------------------------

    [Fact]
    public async Task GetDeliveryQueueStats_EmptyQueue_AllZerosAndNoDivideByZero()
    {
        var repo = new InMemoryActivityPubRepository();
        var service = CreateService(repo);

        var stats = await service.GetDeliveryQueueStatsAsync();

        Assert.Equal(0, stats.PendingCount);
        Assert.Equal(0, stats.FailedCount);
        Assert.Equal(0, stats.MaxRetriesExceededCount);
        Assert.Equal(0, stats.ErrorRate);
        Assert.Null(stats.OldestPending);
        Assert.Null(stats.LastSuccessfulDelivery);
    }

    [Fact]
    public async Task GetDeliveryQueueStats_MixOfAllFiveStatuses_BucketsAndErrorRateCorrect()
    {
        var baseTime = DateTime.UtcNow;
        var repo = new Mock<IActivityPubRepository>();
        var items = new List<SharedInboxDeliveryEntity>
        {
            Delivery(DeliveryStatus.Queued, baseTime.AddMinutes(-30)),
            Delivery(DeliveryStatus.Queued, baseTime.AddMinutes(-10)),
            Delivery(DeliveryStatus.Processing, baseTime.AddMinutes(-20)),
            Delivery(DeliveryStatus.Delivered, baseTime, updated: baseTime.AddMinutes(-1)),
            Delivery(DeliveryStatus.Delivered, baseTime, updated: baseTime.AddMinutes(-5)),
            Delivery(DeliveryStatus.Failed, baseTime, retryCount: 1, failureReason: "500"),
            Delivery(DeliveryStatus.MaxRetriesExceeded, baseTime, retryCount: 5, failureReason: "gave up")
        };
        repo.Setup(r => r.GetPendingSharedInboxDeliveriesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.FromResult<ICollection<SharedInboxDeliveryEntity>>(items));
        var service = CreateService(repo.Object);

        var stats = await service.GetDeliveryQueueStatsAsync();

        // PendingCount = Queued(2) + Processing(1)
        Assert.Equal(3, stats.PendingCount);
        Assert.Equal(1, stats.FailedCount);
        Assert.Equal(1, stats.MaxRetriesExceededCount);
        // OldestPending = min CreatedAt among queued/processing = -30 min
        Assert.Equal(baseTime.AddMinutes(-30), stats.OldestPending);
        // LastSuccessfulDelivery = max UpdatedAt among Delivered = -1 min
        Assert.Equal(baseTime.AddMinutes(-1), stats.LastSuccessfulDelivery);
        // ErrorRate = (1 failed + 1 maxed) / 7 total * 100 = 28.571428...
        Assert.Equal(200.0 / 7, stats.ErrorRate, precision: 10);
    }

    // --- GetRecentErrorsAsync --------------------------------------------

    [Fact]
    public async Task GetRecentErrors_OnlyFailedAndMaxed_OrderedByMostRecentFirst()
    {
        var baseTime = DateTime.UtcNow;
        var repo = new Mock<IActivityPubRepository>();
        var items = new List<SharedInboxDeliveryEntity>
        {
            Delivery(DeliveryStatus.Queued, baseTime, lastAttempt: baseTime),            // excluded
            Delivery(DeliveryStatus.Delivered, baseTime, lastAttempt: baseTime),          // excluded
            Delivery(DeliveryStatus.Failed, baseTime.AddMinutes(-100), lastAttempt: baseTime.AddMinutes(-100), failureReason: "old"),
            Delivery(DeliveryStatus.MaxRetriesExceeded, baseTime.AddMinutes(-10), lastAttempt: baseTime.AddMinutes(-10)),
            Delivery(DeliveryStatus.Failed, baseTime.AddMinutes(-1), lastAttempt: baseTime.AddMinutes(-1), failureReason: "new")
        };
        repo.Setup(r => r.GetPendingSharedInboxDeliveriesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.FromResult<ICollection<SharedInboxDeliveryEntity>>(items));
        var service = CreateService(repo.Object);

        var errors = (await service.GetRecentErrorsAsync()).ToList();

        // Only the 3 failed/maxed items surface, most-recent first.
        Assert.Equal(3, errors.Count);
        Assert.Equal("new", errors[0].FailureReason);
        Assert.Equal("Unknown", errors[1].FailureReason); // null reason -> "Unknown"
        Assert.Equal("old", errors[2].FailureReason);
        Assert.Equal(baseTime.AddMinutes(-1), errors[0].LastAttempt);
    }

    [Fact]
    public async Task GetRecentErrors_RespectsLimit()
    {
        var baseTime = DateTime.UtcNow;
        var repo = new Mock<IActivityPubRepository>();
        var items = Enumerable.Range(0, 5)
            .Select(i => Delivery(DeliveryStatus.Failed, baseTime.AddMinutes(-i), lastAttempt: baseTime.AddMinutes(-i), failureReason: $"r{i}"))
            .ToList();
        repo.Setup(r => r.GetPendingSharedInboxDeliveriesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.FromResult<ICollection<SharedInboxDeliveryEntity>>(items));
        var service = CreateService(repo.Object);

        var errors = (await service.GetRecentErrorsAsync(limit: 2)).ToList();

        Assert.Equal(2, errors.Count);
        // The two most-recent (r0, r1) win.
        Assert.Contains(errors, e => e.FailureReason == "r0");
        Assert.Contains(errors, e => e.FailureReason == "r1");
    }

    // --- GetHealthStatusAsync -> DetermineOverallStatus thresholds -------
    // The private DetermineOverallStatus maps DeliveryQueue stats to a string:
    //   ErrorRate > 50 -> "Critical", > 20 -> "Degraded", PendingCount > 1000 -> "Warning", else "Healthy".
    // GetHealthStatusAsync also populates the EF-backed ActivityProcessing and
    // Database sections, so we feed it an empty InMemory DbContext + a mocked
    // repository that drives the error-rate / pending-count thresholds.

    private static FederationHealthService CreateServiceWithEmptyDb(IActivityPubRepository repository) => CreateService(repository);

    private static Mock<IActivityPubRepository> RepoReturning(IEnumerable<SharedInboxDeliveryEntity> items)
    {
        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.GetPendingSharedInboxDeliveriesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.FromResult<ICollection<SharedInboxDeliveryEntity>>(items.ToList()));
        return repo;
    }

    [Fact]
    public async Task GetHealthStatus_NoFailures_IsHealthy()
    {
        var baseTime = DateTime.UtcNow;
        var repo = RepoReturning(new[]
        {
            Delivery(DeliveryStatus.Queued, baseTime),
            Delivery(DeliveryStatus.Delivered, baseTime, updated: baseTime)
        });

        var status = await CreateServiceWithEmptyDb(repo.Object).GetHealthStatusAsync();

        Assert.Equal(0, status.DeliveryQueue.ErrorRate);
        Assert.Equal("Healthy", status.OverallStatus);
    }

    [Fact]
    public async Task GetHealthStatus_ErrorRateAbove20_IsDegraded()
    {
        // 1 failed out of 4 total = 25% error rate (> 20, <= 50).
        var baseTime = DateTime.UtcNow;
        var repo = RepoReturning(new[]
        {
            Delivery(DeliveryStatus.Queued, baseTime),
            Delivery(DeliveryStatus.Queued, baseTime),
            Delivery(DeliveryStatus.Delivered, baseTime, updated: baseTime),
            Delivery(DeliveryStatus.Failed, baseTime, retryCount: 1)
        });

        var status = await CreateServiceWithEmptyDb(repo.Object).GetHealthStatusAsync();

        Assert.InRange(status.DeliveryQueue.ErrorRate, 20.0001, 50.0);
        Assert.Equal("Degraded", status.OverallStatus);
    }

    [Fact]
    public async Task GetHealthStatus_ErrorRateAbove50_IsCritical()
    {
        // 2 failed out of 3 total = 66.7% error rate (> 50).
        var baseTime = DateTime.UtcNow;
        var repo = RepoReturning(new[]
        {
            Delivery(DeliveryStatus.Failed, baseTime, retryCount: 1),
            Delivery(DeliveryStatus.Failed, baseTime, retryCount: 1),
            Delivery(DeliveryStatus.Queued, baseTime)
        });

        var status = await CreateServiceWithEmptyDb(repo.Object).GetHealthStatusAsync();

        Assert.InRange(status.DeliveryQueue.ErrorRate, 50.0001, 100.0);
        Assert.Equal("Critical", status.OverallStatus);
    }

    [Fact]
    public async Task GetHealthStatus_LargePendingQueueNoFailures_IsWarning()
    {
        // 0% error rate but > 1000 pending -> "Warning".
        var baseTime = DateTime.UtcNow;
        var items = Enumerable.Range(0, 1001)
            .Select(_ => Delivery(DeliveryStatus.Queued, baseTime))
            .ToList();
        var repo = RepoReturning(items);

        var status = await CreateServiceWithEmptyDb(repo.Object).GetHealthStatusAsync();

        Assert.Equal(0, status.DeliveryQueue.ErrorRate);
        Assert.True(status.DeliveryQueue.PendingCount > 1000);
        Assert.Equal("Warning", status.OverallStatus);
    }

    private sealed class NoopHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}
