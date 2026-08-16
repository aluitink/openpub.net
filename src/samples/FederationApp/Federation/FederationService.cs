using ActivityPub.Core;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;
using System.Net.Http.Json;

namespace FederationApp.Federation;

public class FederationService
{
    private readonly HttpClient _httpClient;
    private readonly InstanceManager _instanceManager;

    public FederationService(HttpClient httpClient, InstanceManager instanceManager)
    {
        _httpClient = httpClient;
        _instanceManager = instanceManager;
    }

    public async Task<bool> SendActivityToInstanceAsync(string activityJson, string domain)
    {
        var instances = await _instanceManager.GetInstancesAsync();
        var instance = instances.FirstOrDefault(i => i.Domain == domain);

        if (instance == null)
            return false;

        var inboxUrl = $"https://{domain}/inbox";

        try
        {
            var content = new StringContent(activityJson, System.Text.Encoding.UTF8, "application/ld+json");
            await _httpClient.PostAsync(inboxUrl, content);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<DeliveryStatus>> GetDeliveryStatusAsync()
    {
        var instances = await _instanceManager.GetInstancesAsync();

        return instances.Select(i => new DeliveryStatus
        {
            Domain = i.Domain,
            LastDelivery = i.LastContacted,
            IsActive = i.IsConnected
        }).ToList();
    }
}

public class DeliveryStatus
{
    public string Domain { get; set; } = string.Empty;
    public DateTime LastDelivery { get; set; }
    public bool IsActive { get; set; }
}
