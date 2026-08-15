using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;

namespace ActivityPub.Core.Services;

public interface IWebhookDeliveryService
{
    Task<bool> ConfigureWebhookAsync(string actorId, string eventType, string endpointUrl,
        string httpMethod, bool enabled, string? secretKey = null, int maxRetries = 3,
        int retryDelaySeconds = 60, bool useExponentialBackoff = true);

    Task<bool> DeleteWebhookConfigAsync(int configId);

    Task<ICollection<WebhookConfigEntity>> GetWebhookConfigsAsync(string actorId, string? eventType = null);

    Task DeliverActivityToWebhooksAsync(Activity activity);

    Task ProcessPendingDeliveriesAsync();

    Task<bool> VerifyWebhookSignatureAsync(string secretKey, string payload, string signature);
}
