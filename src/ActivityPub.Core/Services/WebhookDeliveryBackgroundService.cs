using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Services;

public class WebhookDeliveryBackgroundService : BackgroundService
{
    private readonly ILogger<WebhookDeliveryBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public WebhookDeliveryBackgroundService(
        ILogger<WebhookDeliveryBackgroundService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook delivery background service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var deliveryService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();
                
                await deliveryService.ProcessPendingDeliveriesAsync();

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook deliveries");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        _logger.LogInformation("Webhook delivery background service stopping");
    }
}
