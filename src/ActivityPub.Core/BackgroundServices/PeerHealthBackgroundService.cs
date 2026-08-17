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
/// Periodically probes known federation peers (remote ActivityPub servers) for
/// liveness and records the outcomes with <see cref="IPeerHealthService"/>.
/// Peers that are unreachable for
/// <see cref="PeerHealthOptions.AutoBlockProbeFailureThreshold"/> probes in a row
/// are automatically blocked, independent of delivery outcomes.
/// </summary>
public class PeerHealthBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PeerHealthBackgroundService> _logger;
    private readonly Timer _timer;
    private bool _isProcessing;

    public PeerHealthBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<ActivityPubOptions> options,
        ILogger<PeerHealthBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        var intervalMinutes = Math.Max(1, options.Value.PeerHealth.ProbeIntervalMinutes);
        _timer = new Timer(TimeSpan.FromMinutes(intervalMinutes).TotalMilliseconds);
        _timer.AutoReset = true;
        _timer.Elapsed += (_, _) => _ = RunProbeAsync(CancellationToken.None);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SafeLogInformation("PeerHealthBackgroundService starting");

        // Probe shortly after startup so we have a fresh picture of peer
        // health, then continue on the timer.
        _ = RunProbeAsync(stoppingToken);

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
            try { _timer.Dispose(); } catch { /* may already be disposed */ }
            SafeLogInformation("PeerHealthBackgroundService stopping");
        }
    }

    private async Task RunProbeAsync(CancellationToken cancellationToken)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;
            var peerHealth = sp.GetRequiredService<IPeerHealthService>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var options = sp.GetRequiredService<IOptions<ActivityPubOptions>>().Value.PeerHealth;

            if (!options.Enabled)
            {
                return;
            }

            var peers = await peerHealth.GetPeersAsync(onlyBlocked: false);
            if (peers.Count == 0)
            {
                return; // No known peers yet; nothing to probe.
            }

            var httpClient = httpClientFactory.CreateClient("FederationHealth");
            SafeLogInformation("Probing {Count} federation peers for liveness", peers.Count);

            foreach (var peer in peers)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var reachable = await ProbeDomainAsync(httpClient, peer.Domain, cancellationToken);
                await peerHealth.RecordProbeOutcomeAsync(peer.Domain, reachable);
            }

            SafeLogInformation("Federation peer liveness probing completed");
        }
        catch (Exception ex)
        {
            try { _logger?.LogError(ex, "Error probing federation peers for liveness"); }
            catch { /* logger may be disposed during shutdown */ }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task<bool> ProbeDomainAsync(HttpClient httpClient, string domain, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var webfingerUrl = $"https://{domain}/.well-known/webfinger?resource=acct:test@{domain}";
            var response = await httpClient.GetAsync(webfingerUrl, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            return false; // Timed out.
        }
        catch
        {
            return false;
        }
    }

    private void SafeLogInformation(string message, params object[] args)
    {
        try { _logger?.LogInformation(message, args); }
        catch { /* logger may be disposed during shutdown */ }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        try { _logger?.LogInformation("PeerHealthBackgroundService stopping"); } catch { /* ignore */ }
        try { _timer?.Stop(); } catch { /* may already be disposed */ }
        try { _timer?.Dispose(); } catch { /* may already be disposed */ }
        await base.StopAsync(stoppingToken);
    }
}
