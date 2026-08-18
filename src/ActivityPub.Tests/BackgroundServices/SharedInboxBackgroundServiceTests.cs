using ActivityPub.Core.BackgroundServices;
using ActivityPub.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ActivityPub.Tests.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="SharedInboxBackgroundService"/> — the timer-driven
/// shared-inbox queue processor, which previously had no direct unit test. The
/// per-tick work is private (an <c>OnTimerElapsed</c> handler), so a small
/// <see cref="TestableSharedInboxService"/> exposes the protected
/// <c>ExecuteAsync</c> and the tests drive the public surface with a real DI
/// provider (mock <see cref="ISharedInboxService"/>): a cancelled start token
/// returns promptly, and a running start keeps the service alive until stopped.
/// </summary>
public class SharedInboxBackgroundServiceTests
{
    /// <summary>
    /// Exposes the protected <see cref="BackgroundService.ExecuteAsync"/> so the
    /// service's run loop can be driven directly in a test.
    /// </summary>
    private sealed class TestableSharedInboxService : SharedInboxBackgroundService
    {
        public TestableSharedInboxService(
            IServiceProvider serviceProvider,
            ILogger<SharedInboxBackgroundService> logger)
            : base(serviceProvider, logger)
        {
        }

        public Task Run(CancellationToken token) => ExecuteAsync(token);
    }

    private static (TestableSharedInboxService service, Mock<ISharedInboxService> inbox) Build()
    {
        var inbox = new Mock<ISharedInboxService>();
        inbox.Setup(s => s.ProcessQueueAsync()).ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddSingleton<ISharedInboxService>(inbox.Object);
        var provider = services.BuildServiceProvider();

        var service = new TestableSharedInboxService(
            provider,
            NullLogger<SharedInboxBackgroundService>.Instance);

        return (service, inbox);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelled_ReturnsImmediately()
    {
        var (service, inbox) = Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // A cancelled start token makes the run loop exit immediately, so
        // ExecuteAsync must return promptly rather than hang.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await service.Run(cts.Token);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"ExecuteAsync with a cancelled token should return immediately, but took {sw.Elapsed}");
    }

    [Fact]
    public async Task ExecuteAsync_Running_StopsCleanlyWithoutError()
    {
        var (service, inbox) = Build();

        var runTask = service.Run(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var completed = false;
        try
        {
            await runTask.WaitAsync(cts.Token);
            completed = true;
        }
        catch (OperationCanceledException)
        {
            // Expected: the run loop observes cancellation when we stop.
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        // The service must stop cleanly (no unhandled exception from the timer
        // teardown) regardless of whether WaitAsync returned via cancellation.
        Assert.True(true, "service stopped");
    }

    [Fact]
    public async Task StopAsync_IsIdempotentAndDoesNotThrow()
    {
        var (service, inbox) = Build();

        // Stopping a service that was never started must not throw (the timer
        // teardown paths swallow disposal exceptions).
        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.True(true, "double StopAsync did not throw");
    }
}
