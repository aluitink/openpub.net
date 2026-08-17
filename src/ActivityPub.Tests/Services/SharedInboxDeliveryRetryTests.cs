using ActivityPub.Core.Caching;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Phase 38 Task 2 — outbound delivery retry with exponential backoff.
/// Exercises <see cref="SharedInboxService.ProcessQueueAsync"/> against a
/// mocked outbound sender and the in-memory repository, verifying the retry
/// count, backoff gating (NextRetryAt), and terminal dead-letter state.
/// </summary>
public class SharedInboxDeliveryRetryTests
{
    private const string ActivityJson =
        """{"id":"urn:activity:1","type":"Create","actor":"https://local.example/users/alice#main-key"}""";
    private const string Target = "https://remote.example/users/bob#main-key";

    private static SharedInboxService CreateService(
        Mock<IOutboundActivityService> outbound,
        InMemoryActivityPubRepository repo,
        DeliveryRetryOptions retryOptions)
    {
        var options = new ActivityPubOptions { DeliveryRetry = retryOptions };
        return new SharedInboxService(
            repo,
            outbound.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IFederationCache>(),
            Mock.Of<ILogger<SharedInboxService>>(),
            Options.Create(options));
    }

    private static Mock<IOutboundActivityService> OutboundThat(bool success)
    {
        var mock = new Mock<IOutboundActivityService>();
        mock
            .Setup(s => s.SendActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(success);
        return mock;
    }

    /// <summary>
    /// Seeds a local sender actor (username "alice") with a private key into
    /// the repository so that <c>SharedInboxService</c> can sign outbound
    /// deliveries. The actor ID matches the one referenced by
    /// <see cref="ActivityJson"/>.
    /// </summary>
    private static async Task SeedSenderActorAsync(InMemoryActivityPubRepository repo)
    {
        var actor = new ActivityPub.Core.Models.Actor
        {
            Id = "https://local.example/users/alice#main-key",
            PreferredUsername = "alice",
            AdditionalProperties = new System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>
            {
                ["privateKeyPem"] = System.Text.Json.JsonSerializer.SerializeToElement("test-private-key-pem")
            }
        };
        await repo.SaveUserActorAsync(actor);
    }

    /// <summary>
    /// Queues a delivery and captures the entity reference the repository
    /// holds. Because <c>UpdateSharedInboxDeliveryAsync</c> mutates that same
    /// instance in place, the returned reference is a stable handle to the row's
    /// final state.
    /// </summary>
    private static async Task<SharedInboxDeliveryEntity> QueueAndCapture(InMemoryActivityPubRepository repo, string activityId)
    {
        Assert.True(await repo.QueueSharedInboxDeliveryAsync(activityId, ActivityJson, Target));
        var pending = await repo.GetPendingSharedInboxDeliveriesAsync(10, 10);
        var item = Assert.Single(pending, d => d.ActivityId == activityId);
        return item;
    }

    [Fact]
    public async Task SuccessfulDelivery_IsMarkedDelivered_NoRetry()
    {
        var repo = new InMemoryActivityPubRepository();
        await SeedSenderActorAsync(repo);
        var item = await QueueAndCapture(repo, "act-1");
        var service = CreateService(OutboundThat(true), repo, new DeliveryRetryOptions());

        await service.ProcessQueueAsync();

        Assert.Equal(DeliveryStatus.Delivered, item.Status);
        Assert.Equal(0, item.RetryCount);
        Assert.Null(item.NextRetryAt);
    }

    [Fact]
    public async Task FailedDelivery_IsMarkedFailed_WithBackoffNextRetryAt()
    {
        var repo = new InMemoryActivityPubRepository();
        await SeedSenderActorAsync(repo);
        var item = await QueueAndCapture(repo, "act-2");
        var before = DateTime.UtcNow;
        var service = CreateService(OutboundThat(false), repo, new DeliveryRetryOptions { BaseRetryDelaySeconds = 30 });

        await service.ProcessQueueAsync();

        Assert.Equal(DeliveryStatus.Failed, item.Status);
        Assert.Equal(1, item.RetryCount);
        Assert.NotNull(item.NextRetryAt);

        // First retry delay = base (30s): attemptNumber-1 = 0 -> 2^0 = 1x.
        var delay = (item.NextRetryAt!.Value - before).TotalSeconds;
        Assert.InRange(delay, 29, 31);
    }

    [Fact]
    public async Task RepeatedFailures_GrowExponentially_UntilMaxRetries()
    {
        var repo = new InMemoryActivityPubRepository();
        await SeedSenderActorAsync(repo);
        var item = await QueueAndCapture(repo, "act-3");
        var retryOptions = new DeliveryRetryOptions
        {
            MaxRetries = 3,
            BaseRetryDelaySeconds = 10,
            UseExponentialBackoff = true,
            MaxRetryDelaySeconds = 1000
        };
        var service = CreateService(OutboundThat(false), repo, retryOptions);

        // Attempt 1: fails -> Failed, RetryCount=1, NextRetryAt ~now+10s.
        await service.ProcessQueueAsync();
        Assert.Equal(DeliveryStatus.Failed, item.Status);
        Assert.Equal(1, item.RetryCount);
        Assert.InRange((item.NextRetryAt!.Value - DateTime.UtcNow).TotalSeconds, 9, 11);

        // Now gated by a future NextRetryAt: a second call must NOT re-attempt.
        await service.ProcessQueueAsync();
        Assert.Equal(1, item.RetryCount);

        // Fast-forward past the backoff window; second failure -> RetryCount=2,
        // NextRetryAt ~now+20s (2x base).
        item.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
        await repo.UpdateSharedInboxDeliveryAsync(item);
        await service.ProcessQueueAsync();
        Assert.Equal(DeliveryStatus.Failed, item.Status);
        Assert.Equal(2, item.RetryCount);
        Assert.InRange((item.NextRetryAt!.Value - DateTime.UtcNow).TotalSeconds, 19, 21);

        // Fast-forward again; third failure reaches MaxRetries -> terminal.
        item.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
        await repo.UpdateSharedInboxDeliveryAsync(item);
        await service.ProcessQueueAsync();
        Assert.Equal(DeliveryStatus.MaxRetriesExceeded, item.Status);
        Assert.Equal(3, item.RetryCount);
        Assert.Null(item.NextRetryAt);
    }

    [Fact]
    public async Task FlatBackoff_UsesConstantDelay()
    {
        var repo = new InMemoryActivityPubRepository();
        await SeedSenderActorAsync(repo);
        var item = await QueueAndCapture(repo, "act-4");
        var before = DateTime.UtcNow;
        var service = CreateService(
            OutboundThat(false),
            repo,
            new DeliveryRetryOptions { MaxRetries = 5, BaseRetryDelaySeconds = 15, UseExponentialBackoff = false });

        await service.ProcessQueueAsync();

        Assert.Equal(DeliveryStatus.Failed, item.Status);
        Assert.InRange((item.NextRetryAt!.Value - before).TotalSeconds, 14, 16);
    }

    [Fact]
    public async Task BackoffDelay_IsCappedAtMaxRetryDelaySeconds()
    {
        var repo = new InMemoryActivityPubRepository();
        await SeedSenderActorAsync(repo);
        var item = await QueueAndCapture(repo, "act-5");
        var before = DateTime.UtcNow;
        var service = CreateService(
            OutboundThat(false),
            repo,
            new DeliveryRetryOptions
            {
                MaxRetries = 5,
                BaseRetryDelaySeconds = 10,
                UseExponentialBackoff = true,
                MaxRetryDelaySeconds = 20 // cap at 20s
            });

        await service.ProcessQueueAsync();

        // attempt 1 -> 10s (under cap)
        Assert.InRange((item.NextRetryAt!.Value - before).TotalSeconds, 9, 11);

        // Fast-forward and fail again: attempt 2 -> would be 20s, at cap.
        item.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
        await repo.UpdateSharedInboxDeliveryAsync(item);
        before = DateTime.UtcNow;
        await service.ProcessQueueAsync();
        Assert.InRange((item.NextRetryAt!.Value - before).TotalSeconds, 19, 21);

        // Fast-forward and fail a third time: attempt 3 -> would be 40s but capped at 20s.
        item.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
        await repo.UpdateSharedInboxDeliveryAsync(item);
        before = DateTime.UtcNow;
        await service.ProcessQueueAsync();
        Assert.InRange((item.NextRetryAt!.Value - before).TotalSeconds, 19, 21);
    }

    [Fact]
    public async Task PendingQuery_ExcludesFailedItem_WhoseBackoffHasNotElapsed()
    {
        var repo = new InMemoryActivityPubRepository();
        var item = await QueueAndCapture(repo, "act-6");

        // Put the row in a Failed state with a future NextRetryAt.
        item.Status = DeliveryStatus.Failed;
        item.RetryCount = 1;
        item.NextRetryAt = DateTime.UtcNow.AddMinutes(5);
        await repo.UpdateSharedInboxDeliveryAsync(item);

        // Not eligible yet.
        var notYet = await repo.GetPendingSharedInboxDeliveriesAsync(10, 5);
        Assert.DoesNotContain(notYet, d => d.ActivityId == "act-6");

        // Eligible once the backoff window has passed.
        item.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
        await repo.UpdateSharedInboxDeliveryAsync(item);
        var now = await repo.GetPendingSharedInboxDeliveriesAsync(10, 5);
        Assert.Contains(now, d => d.ActivityId == "act-6");
    }

    [Fact]
    public async Task PendingQuery_ExcludesItem_ThatExhaustedMaxRetries()
    {
        var repo = new InMemoryActivityPubRepository();
        var item = await QueueAndCapture(repo, "act-7");

        // Exhaust the retry cap.
        item.Status = DeliveryStatus.Failed;
        item.RetryCount = 5; // >= maxRetries (5)
        item.NextRetryAt = null;
        await repo.UpdateSharedInboxDeliveryAsync(item);

        var pending = await repo.GetPendingSharedInboxDeliveriesAsync(10, 5);
        Assert.DoesNotContain(pending, d => d.ActivityId == "act-7");
    }
}
