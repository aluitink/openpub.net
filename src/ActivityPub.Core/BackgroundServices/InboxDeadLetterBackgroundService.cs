using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ActivityPub.Core.BackgroundServices;

/// <summary>
/// Periodically re-processes dead-lettered inbound activities (ones that
/// exhausted their retry budget after being delivered to our inbox) and prunes
/// dead-letter rows that are older than
/// <see cref="InboxProcessingOptions.DlqRetentionDays"/>. Mirrors the shape of
/// <see cref="PeerHealthBackgroundService"/>: a timer-driven loop that opens a
/// DI scope per run and swallows its own exceptions so a failure in one cycle
/// cannot kill the host.
/// </summary>
public class InboxDeadLetterBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxDeadLetterBackgroundService> _logger;
    private readonly Timer _timer;
    private bool _isProcessing;

    public InboxDeadLetterBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<ActivityPubOptions> options,
        ILogger<InboxDeadLetterBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Re-process dead-letter items every 10 minutes. The interval is not a
        // tunable option (re-processing is idempotent and cheap); 10 minutes
        // is a reasonable default that gives operators time to inspect the
        // DLQ before items are auto-replayed.
        _timer = new Timer(TimeSpan.FromMinutes(10).TotalMilliseconds);
        _timer.AutoReset = true;
        _timer.Elapsed += (_, _) => _ = RunCycleAsync(CancellationToken.None);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SafeLogInformation("InboxDeadLetterBackgroundService starting");

        // Run once shortly after startup so recently dead-lettered items get a
        // chance to recover (e.g. a transient database outage has cleared),
        // then continue on the timer.
        _ = RunCycleAsync(stoppingToken);

        _timer.Start();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        finally
        {
            try { _timer.Stop(); } catch { /* may already be disposed */ }
            SafeLogInformation("InboxDeadLetterBackgroundService stopping");
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;
            var sharedInboxService = sp.GetRequiredService<ISharedInboxService>();
            var repository = sp.GetRequiredService<IActivityPubRepository>();
            var options = sp.GetRequiredService<IOptions<ActivityPubOptions>>().Value.InboxProcessing;

            if (!options.Enabled)
            {
                return;
            }

            // Re-process dead-lettered items.
            var replayed = await sharedInboxService.ProcessInboxDeadLettersAsync(batchSize: 100);
            if (replayed > 0)
            {
                SafeLogInformation("Re-processed {Replayed} dead-lettered inbound activities", replayed);
            }

            // Prune dead-letter rows past their retention window.
            if (options.DlqRetentionDays > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-options.DlqRetentionDays);
                var pruned = await repository.PruneInboxDeadLettersAsync(cutoff);
                if (pruned > 0)
                {
                    SafeLogInformation("Pruned {Pruned} dead-lettered inbound activities older than {RetentionDays} days",
                        pruned, options.DlqRetentionDays);
                }
            }
        }
        catch (Exception ex)
        {
            try { _logger?.LogError(ex, "Error running inbound dead-letter cycle"); }
            catch { /* logger may be disposed during shutdown */ }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void SafeLogInformation(string message, params object[] args)
    {
        try { _logger?.LogInformation(message, args); }
        catch { /* logger may be disposed during shutdown */ }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        try { _logger?.LogInformation("InboxDeadLetterBackgroundService stopping"); } catch { /* ignore */ }
        try { _timer?.Stop(); } catch { /* may already be disposed */ }
        await base.StopAsync(stoppingToken);
    }
}
