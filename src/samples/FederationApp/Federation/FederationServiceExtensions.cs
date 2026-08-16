using ActivityPub.Core;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;
using System.Net.Http.Json;

namespace FederationApp.Federation;

public static class FederationServiceExtensions
{
    public static async Task DeliverToInstanceAsync(this FederationService service, string domain, string actorId)
    {
        var instances = await GetInstancesAsync(service);
        var instance = instances.FirstOrDefault(i => i.Domain == domain);

        if (instance == null || !instance.IsConnected)
            return;

        var inboxUrl = $"https://{domain}/inbox";

        try
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

            var activityJson = System.Text.Json.JsonSerializer.Serialize(activity);
            await service.SendActivityToInstanceAsync(activityJson, domain);
        }
        catch
        {
        }
    }

    private static async Task<List<InstanceInfo>> GetInstancesAsync(FederationService service)
    {
        var mgr = GetInstanceManager(service);
        return mgr != null ? await mgr.GetInstancesAsync() : new List<InstanceInfo>();
    }

    private static InstanceManager? GetInstanceManager(FederationService service)
    {
        return service switch
        {
            { } s when s.GetType().GetField("_instanceManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(s) is InstanceManager mgr => mgr,
            _ => null
        };
    }
}
