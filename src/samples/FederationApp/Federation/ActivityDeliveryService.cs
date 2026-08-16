using ActivityPub.Core;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FederationApp.Federation;

public class ActivityDeliveryService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public ActivityDeliveryService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var instanceManager = scope.ServiceProvider.GetRequiredService<InstanceManager>();
                var federationService = scope.ServiceProvider.GetRequiredService<FederationService>();

                var instances = await instanceManager.GetConnectedInstancesAsync();

                foreach (var instance in instances)
                {
                    await federationService.SendActivityToInstanceAsync(
                        CreateTestActivityJson(instance.ActorId),
                        instance.Domain);
                }
            }
            catch
            {
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private static string CreateTestActivityJson(string actorId)
    {
        var activity = new Activity
        {
            Id = $"https://localhost/activities/{Guid.NewGuid()}",
            Type = "Create",
            Actor = actorId,
            Object = new global::ActivityPub.Core.Models.Object
            {
                Id = $"https://localhost/objects/{Guid.NewGuid()}",
                Type = "Note",
                Content = "Test federation message"
            }
        };

        return System.Text.Json.JsonSerializer.Serialize(activity);
    }
}
