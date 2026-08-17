using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace ActivityPub.Core.Services;

public class WebhookDeliveryService : IWebhookDeliveryService
{
    private readonly IActivityPubRepository _repository;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public WebhookDeliveryService(IActivityPubRepository repository, HttpClient httpClient)
    {
        _repository = repository;
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    public async Task<bool> ConfigureWebhookAsync(string actorId, string eventType, string endpointUrl,
        string httpMethod, bool enabled, string? secretKey = null, int maxRetries = 3,
        int retryDelaySeconds = 60, bool useExponentialBackoff = true)
    {
        var existingConfigs = await _repository.GetWebhookConfigsAsync(actorId, eventType);

        WebhookConfigEntity config;

        if (existingConfigs.Any())
        {
            config = existingConfigs.First();
        }
        else
        {
            config = new WebhookConfigEntity
            {
                ActorId = actorId,
                EventType = eventType,
                EndpointUrl = endpointUrl,
                HttpMethod = httpMethod,
                Enabled = enabled,
                SecretKey = secretKey,
                MaxRetries = maxRetries,
                RetryDelaySeconds = retryDelaySeconds,
                UseExponentialBackoff = useExponentialBackoff,
                DeliveryMethod = httpMethod switch
                {
                    "POST" => WebhookDeliveryMethod.HttpPost,
                    "PUT" => WebhookDeliveryMethod.HttpPut,
                    _ => WebhookDeliveryMethod.HttpPost
                }
            };
        }

        config.EndpointUrl = endpointUrl;
        config.HttpMethod = httpMethod;
        config.Enabled = enabled;
        config.SecretKey = secretKey;
        config.MaxRetries = maxRetries;
        config.RetryDelaySeconds = retryDelaySeconds;
        config.UseExponentialBackoff = useExponentialBackoff;

        return await _repository.SaveWebhookConfigAsync(config);
    }

    public async Task<bool> DeleteWebhookConfigAsync(int configId)
    {
        return await _repository.DeleteWebhookConfigAsync(configId);
    }

    public async Task<ICollection<WebhookConfigEntity>> GetWebhookConfigsAsync(string actorId, string? eventType = null)
    {
        return await _repository.GetWebhookConfigsAsync(actorId, eventType);
    }

    public async Task DeliverActivityToWebhooksAsync(Activity activity)
    {
        var actorId = activity.ActorId ?? (activity.AdditionalProperties?.TryGetValue("attributedTo", out var attributedTo) == true ? (attributedTo.ValueKind == JsonValueKind.String ? attributedTo.GetString() : attributedTo.ToString()) : null) ?? string.Empty;

        if (string.IsNullOrEmpty(actorId))
        {
            return;
        }

        var configs = await _repository.GetWebhookConfigsAsync(actorId);

        foreach (var config in configs.Where(c => c.Enabled))
        {
            var shouldDeliver = config.EventType == "All" ||
                               config.EventType == activity.Type ||
                               config.EventType == "Create" && activity.Type == "Create";

            if (shouldDeliver)
            {
                var delivery = new WebhookDeliveryEntity
                {
                    ConfigId = config.Id.ToString(),
                    ActivityId = activity.Id ?? string.Empty,
                    ActivityJson = JsonSerializer.Serialize(activity, _jsonOptions),
                    ActorId = actorId,
                    Status = WebhookDeliveryStatus.Queued
                };

                await _repository.QueueWebhookDeliveryAsync(delivery);
            }
        }
    }

    public async Task ProcessPendingDeliveriesAsync()
    {
        var pendingDeliveries = await _repository.GetPendingWebhookDeliveriesAsync(100);

        foreach (var delivery in pendingDeliveries)
        {
            var config = await _repository.GetWebhookConfigByIdAsync(int.Parse(delivery.ConfigId));
            if (config == null)
            {
                delivery.Status = WebhookDeliveryStatus.Failed;
                delivery.FailureReason = "Webhook configuration not found";
                await _repository.UpdateWebhookDeliveryAsync(delivery);
                continue;
            }

            try
            {
                var result = await DeliverToWebhookAsync(config, delivery);

                if (result)
                {
                    delivery.Status = WebhookDeliveryStatus.Delivered;
                    await _repository.UpdateWebhookDeliveryAsync(delivery);
                    await SaveDeliveryHistoryAsync(delivery, true, 200, string.Empty);
                }
                else
                {
                    HandleDeliveryFailure(config, delivery);
                }
            }
            catch (Exception ex)
            {
                delivery.FailureReason = ex.Message;
                HandleDeliveryFailure(config, delivery);
            }
        }
    }

    private async Task<bool> DeliverToWebhookAsync(WebhookConfigEntity config, WebhookDeliveryEntity delivery)
    {
        var activityJson = delivery.ActivityJson;

        var content = new StringContent(activityJson, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(new HttpMethod(config.HttpMethod), config.EndpointUrl)
        {
            Content = content
        };

        request.Headers.Add("Content-Type", "application/json");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("DemoApp", "1.0"));

        if (!string.IsNullOrEmpty(config.SecretKey))
        {
            var signature = GenerateSignature(config.SecretKey, activityJson);
            request.Headers.Add("X-Webhook-Signature", signature);
        }

        var response = await _httpClient.SendAsync(request);

        delivery.HttpResponseCode = (int)response.StatusCode;
        delivery.UpdatedAt = DateTime.UtcNow;

        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            await SaveDeliveryHistoryAsync(delivery, true, (int)response.StatusCode, responseBody);
            return true;
        }
        else
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            await SaveDeliveryHistoryAsync(delivery, false, (int)response.StatusCode, responseBody);
            return false;
        }
    }

    private void HandleDeliveryFailure(WebhookConfigEntity config, WebhookDeliveryEntity delivery)
    {
        if (delivery.RetryCount >= config.MaxRetries)
        {
            delivery.Status = WebhookDeliveryStatus.MaxRetriesExceeded;
        }
        else
        {
            var delay = config.UseExponentialBackoff
                ? TimeSpan.FromSeconds(config.RetryDelaySeconds * Math.Pow(2, delivery.RetryCount))
                : TimeSpan.FromSeconds(config.RetryDelaySeconds);

            delivery.RetryCount++;
            delivery.LastDeliveryAttempt = DateTime.UtcNow;
            delivery.Status = WebhookDeliveryStatus.Failed;
        }

        _ = _repository.UpdateWebhookDeliveryAsync(delivery);
    }

    private async Task SaveDeliveryHistoryAsync(WebhookDeliveryEntity delivery, bool success, int statusCode, string responseBody)
    {
        var history = new WebhookDeliveryHistoryEntity
        {
            DeliveryId = delivery.Id,
            EventType = "WebhookDelivery",
            RequestHeaders = $"{{\"Content-Type\":\"application/json\"}}",
            RequestBody = delivery.ActivityJson,
            ResponseHeaders = $"{{\"Status-Code\":{statusCode}}}",
            ResponseBody = responseBody,
            HttpResponseCode = statusCode
        };

        await _repository.SaveWebhookDeliveryHistoryAsync(history);
    }

    private string GenerateSignature(string secretKey, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    public async Task<bool> VerifyWebhookSignatureAsync(string secretKey, string payload, string signature)
    {
        try
        {
            var expectedSignature = GenerateSignature(secretKey, payload);
            return signature == expectedSignature;
        }
        catch
        {
            return false;
        }
    }
}
