using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ActivityPub.Core.Services;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ActivityPub.Core.BackgroundServices;

public class SharedInboxBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SharedInboxBackgroundService> _logger;
    private readonly Timer _timer;
    private bool _isProcessing;

    public SharedInboxBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SharedInboxBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
        _timer.AutoReset = true;
        _timer.Elapsed += OnTimerElapsed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SafeLogInformation("SharedInboxBackgroundService starting");

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
            try
            {
                _timer.Stop();
            }
            catch
            {
                // Timer may already be disposed; ignore.
            }
            // Dispose the timer (and its Elapsed delegate closure) so the
            // System.Threading.Timer it wraps can be reclaimed. Without this the
            // timer and the service reference it holds would be left to
            // finalization.
            try
            {
                _timer.Dispose();
            }
            catch
            {
                // Timer may already be disposed; ignore.
            }
            SafeLogInformation("SharedInboxBackgroundService stopping");
        }
    }

    private void SafeLogInformation(string message)
    {
        try
        {
            _logger?.LogInformation(message);
        }
        catch
        {
            // Logger may already be disposed during shutdown; ignore.
        }
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISharedInboxService>();

            SafeLogInformation("Processing shared inbox queue");
            await service.ProcessQueueAsync();
            SafeLogInformation("Shared inbox queue processing completed");
        }
        catch (Exception ex)
        {
            try
            {
                _logger?.LogError(ex, "Error processing shared inbox queue");
            }
            catch
            {
                // Logger may already be disposed; ignore.
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger?.LogInformation("SharedInboxBackgroundService stopping");
        }
        catch
        {
            // Logger may already be disposed during test/host shutdown; ignore.
        }

        try
        {
            _timer?.Stop();
        }
        catch
        {
            // Timer may already be disposed; ignore.
        }

        try
        {
            _timer?.Dispose();
        }
        catch
        {
            // Timer may already be disposed; ignore.
        }

        await base.StopAsync(stoppingToken);
    }
}
