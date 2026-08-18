using ActivityPub.Core.BackgroundServices;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ActivityPub.Tests.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="InboxDeadLetterBackgroundService"/> — the
/// timer-driven dead-letter re-process/prune service, which previously had no
/// direct unit test. The per-cycle work is private, so these tests exercise the
/// public surface via a <see cref="TestableDeadLetterService"/> that exposes the
/// protected <c>ExecuteAsync</c>: a cancelled start token returns immediately and
/// the cycle never runs; a running start triggers the startup cycle (observable
/// through the mocked <see cref="ISharedInboxService.ProcessInboxDeadLettersAsync"/>
/// / <see cref="IActivityPubRepository.PruneInboxDeadLettersAsync"/> calls); and
/// a disabled option short-circuits before any repository work.
/// </summary>
public class InboxDeadLetterBackgroundServiceTests
{
    /// <summary>
    /// Exposes the protected <see cref="BackgroundService.ExecuteAsync"/> so the
    /// service's run loop can be driven directly in a test.
    /// </summary>
    private sealed class TestableDeadLetterService : InboxDeadLetterBackgroundService
    {
        public TestableDeadLetterService(
            IServiceProvider serviceProvider,
            IOptions<ActivityPubOptions> options,
            ILogger<InboxDeadLetterBackgroundService> logger)
            : base(serviceProvider, options, logger)
        {
        }

        public Task Run(CancellationToken token) => ExecuteAsync(token);
    }

    private static IOptions<ActivityPubOptions> BuildOptions(InboxProcessingOptions options)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ActivityPubOptions:InboxProcessing:Enabled"] = options.Enabled.ToString(),
                ["ActivityPubOptions:InboxProcessing:DlqRetentionDays"] = options.DlqRetentionDays.ToString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions();
        services.Configure<ActivityPubOptions>(configuration.GetSection("ActivityPubOptions"));
        return services.BuildServiceProvider().GetRequiredService<IOptions<ActivityPubOptions>>();
    }

    private static (TestableDeadLetterService service, Mock<ISharedInboxService> inbox, Mock<IActivityPubRepository> repo) Build(
        InboxProcessingOptions options)
    {
        var inbox = new Mock<ISharedInboxService>();
        inbox.Setup(s => s.ProcessInboxDeadLettersAsync(It.IsAny<int>()))
             .ReturnsAsync(0);

        var repo = new Mock<IActivityPubRepository>();
        repo.Setup(r => r.PruneInboxDeadLettersAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var services = new ServiceCollection();
        services.AddSingleton<ISharedInboxService>(inbox.Object);
        services.AddSingleton<IActivityPubRepository>(repo.Object);
        var optionsInstance = BuildOptions(options);
        services.AddSingleton(optionsInstance);
        var provider = services.BuildServiceProvider();

        var service = new TestableDeadLetterService(
            provider,
            optionsInstance,
            NullLogger<InboxDeadLetterBackgroundService>.Instance);

        return (service, inbox, repo);
    }

    private static async Task WaitFor(Func<bool> condition, TimeSpan timeout, string what)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < timeout)
            await Task.Delay(50);
        Assert.True(condition(), $"timed out waiting for {what}");
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelled_ReturnsImmediately()
    {
        var (service, inbox, repo) = Build(new InboxProcessingOptions { Enabled = true, DlqRetentionDays = 7 });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // A cancelled start token makes the run loop exit immediately, so
        // ExecuteAsync must return promptly rather than hang. (The startup cycle
        // is still kicked off fire-and-forget — only the loop respects the token.)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await service.Run(cts.Token);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"ExecuteAsync with a cancelled token should return immediately, but took {sw.Elapsed}");
    }

    [Fact]
    public async Task ExecuteAsync_Running_StartsServiceAndRunsStartupCycle()
    {
        var (service, inbox, repo) = Build(new InboxProcessingOptions { Enabled = true, DlqRetentionDays = 7 });

        var runTask = service.Run(CancellationToken.None);

        // The startup cycle fires shortly after start; wait for it to be observed.
        await WaitFor(() =>
        {
            inbox.Verify(s => s.ProcessInboxDeadLettersAsync(It.IsAny<int>()), Times.AtLeastOnce());
            return true;
        }, TimeSpan.FromSeconds(10), "the startup dead-letter cycle to run");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await runTask.WaitAsync(cts.Token); }
        catch (OperationCanceledException) { /* expected on stop */ }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task ExecuteAsync_DisabledOption_DoesNotInvokeRepositoryWork()
    {
        // Enabled = false: the cycle returns before doing any repository work.
        var (service, inbox, repo) = Build(new InboxProcessingOptions { Enabled = false, DlqRetentionDays = 7 });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await service.Run(cts.Token);

        inbox.Verify(s => s.ProcessInboxDeadLettersAsync(It.IsAny<int>()), Times.Never);
        repo.Verify(r => r.PruneInboxDeadLettersAsync(It.IsAny<DateTime>()), Times.Never);
    }
}
