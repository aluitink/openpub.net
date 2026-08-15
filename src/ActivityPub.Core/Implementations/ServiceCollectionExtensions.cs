using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace ActivityPub.Core.Implementations;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebhookServices(this IServiceCollection services)
    {
        services.AddScoped<IWebhookDeliveryService, WebhookDeliveryService>();
        return services;
    }
}
