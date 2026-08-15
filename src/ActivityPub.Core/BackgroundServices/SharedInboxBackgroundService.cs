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
        _logger.LogInformation("SharedInboxBackgroundService starting");

        _timer.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        _timer.Stop();
        _logger.LogInformation("SharedInboxBackgroundService stopping");
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_isProcessing)
        {
            _logger.LogDebug("Previous processing still in progress, skipping this interval");
            return;
        }

        _isProcessing = true;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISharedInboxService>();

            _logger.LogInformation("Processing shared inbox queue");
            await service.ProcessQueueAsync();
            _logger.LogInformation("Shared inbox queue processing completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing shared inbox queue");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SharedInboxBackgroundService stopping");
        _timer.Stop();
        await base.StopAsync(stoppingToken);
    }
}
