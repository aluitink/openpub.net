using ActivityPub.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ActivityPub.Tests.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="WebhookDeliveryBackgroundService"/> — the polling
/// webhook delivery loop, which previously had no direct unit test. The
/// per-tick work is private (the body of the <c>ExecuteAsync</c> loop), so a
/// small <see cref="TestableWebhookDeliveryService"/> exposes the protected
/// <c>ExecuteAsync</c> and the tests drive the public surface with a real
/// scoped DI provider (mock <see cref="IWebhookDeliveryService"/>).
///
/// Note: <c>BackgroundService.StopAsync</c> only cancels the internal stopping
/// token and the loop is typically parked in its 10 s <c>Task.Delay</c>, so
/// StopAsync alone does not promptly end the run task. The "running" tests
/// therefore end the loop via the external cancellation token passed to
/// <c>ExecuteAsync</c> (which the loop observes immediately) and also assert the
/// run task never faults.
/// </summary>
public class WebhookDeliveryBackgroundServiceTests
{
    /// <summary>
    /// Exposes the protected <see cref="BackgroundService.ExecuteAsync"/> so the
    /// service's run loop can be driven directly in a test.
    /// </summary>
    private sealed class TestableWebhookDeliveryService : WebhookDeliveryBackgroundService
    {
        public TestableWebhookDeliveryService(
            ILogger<WebhookDeliveryBackgroundService> logger,
            IServiceScopeFactory scopeFactory)
            : base(logger, scopeFactory)
        {
        }

        public Task Run(CancellationToken token) => ExecuteAsync(token);
    }

    private static (TestableWebhookDeliveryService service, Mock<IWebhookDeliveryService> delivery,
        ServiceProvider provider) Build()
    {
        var delivery = new Mock<IWebhookDeliveryService>();
        delivery.Setup(d => d.ProcessPendingDeliveriesAsync()).Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<WebhookDeliveryBackgroundService>>(
            NullLogger<WebhookDeliveryBackgroundService>.Instance);
        services.AddSingleton<IWebhookDeliveryService>(delivery.Object);
        var provider = services.BuildServiceProvider();

        var service = new TestableWebhookDeliveryService(
            provider.GetRequiredService<ILogger<WebhookDeliveryBackgroundService>>(),
            provider.GetRequiredService<IServiceScopeFactory>());

        return (service, delivery, provider);
    }

    private static int TickCount(Mock<IWebhookDeliveryService> delivery) =>
        delivery.Invocations.Count(c => c.Method.Name == nameof(IWebhookDeliveryService.ProcessPendingDeliveriesAsync));

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelled_ReturnsImmediately()
    {
        var (service, delivery, provider) = Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // A cancelled start token makes the loop exit before its first tick, so
        // ExecuteAsync must return promptly rather than hang.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await service.Run(cts.Token);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"ExecuteAsync with a cancelled token should return immediately, but took {sw.Elapsed}");
        // No delivery work should have run.
        delivery.Verify(d => d.ProcessPendingDeliveriesAsync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Running_ProcessesDeliveriesThenExitsOnCancellation()
    {
        var (service, delivery, provider) = Build();

        using var cts = new CancellationTokenSource();
        var runTask = service.Run(cts.Token);

        // The first tick runs before the 10 s delay, so it should be observed
        // quickly.
        for (var i = 0; i < 50 && TickCount(delivery) == 0; i++)
        {
            await Task.Delay(100);
        }

        // End the loop via the external token (observed immediately, unlike the
        // internal stopping token which the loop may be parked in a delay for).
        // Cancelling while the loop is in its Task.Delay surfaces an
        // OperationCanceledException from ExecuteAsync, which is the normal,
        // clean exit for a cancelled background service.
        cts.Cancel();

        try
        {
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected clean exit when the loop was parked in its delay.
        }

        // At least one delivery pass occurred, and the run loop exited (it did
        // not hang and did not fault with a non-cancellation exception).
        Assert.True(TickCount(delivery) >= 1, "at least one delivery pass should have run");
        Assert.False(runTask.IsFaulted, "the run loop must exit cleanly on cancellation");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeliveryThrows_ContinuesLoopWithoutPropagating()
    {
        var delivery = new Mock<IWebhookDeliveryService>();
        delivery.Setup(d => d.ProcessPendingDeliveriesAsync())
            .ThrowsAsync(new InvalidOperationException("delivery failed"));

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<WebhookDeliveryBackgroundService>>(
            NullLogger<WebhookDeliveryBackgroundService>.Instance);
        services.AddSingleton<IWebhookDeliveryService>(delivery.Object);
        var provider = services.BuildServiceProvider();

        var service = new TestableWebhookDeliveryService(
            provider.GetRequiredService<ILogger<WebhookDeliveryBackgroundService>>(),
            provider.GetRequiredService<IServiceScopeFactory>());

        using var cts = new CancellationTokenSource();
        var runTask = service.Run(cts.Token);

        // The first tick throws; the loop catches it, logs, and sleeps 1 s,
        // then retries. Verify the run task is never faulted and that a second
        // attempt is made.
        for (var i = 0; i < 80 && TickCount(delivery) < 2; i++)
        {
            await Task.Delay(100);
        }

        Assert.False(runTask.IsFaulted,
            "the run loop must not fault when a delivery attempt throws");
        Assert.True(TickCount(delivery) >= 2,
            "the loop must keep retrying after a delivery failure");

        cts.Cancel();
        try
        {
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected on stop.
        }
    }

    [Fact]
    public async Task StopAsync_IsIdempotentAndDoesNotThrow()
    {
        var (service, delivery, provider) = Build();

        // Stopping a service that was never started must not throw.
        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.True(true, "double StopAsync did not throw");
    }
}
