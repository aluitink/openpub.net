using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Services;

public interface IWebFingerService
{
    Task<Actor?> ResolveActorAsync(string handle);
}

public class WebFingerService : IWebFingerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebFingerService> _logger;

    public WebFingerService(HttpClient httpClient, ILogger<WebFingerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Actor?> ResolveActorAsync(string handle)
    {
        var parts = handle.Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            _logger.LogWarning("Invalid handle format: {Handle}", handle);
            return null;
        }

        var username = parts[0];
        var domain = parts[1];
        var resource = $"acct:{username}@{domain}";

        var webfingerUrl = $"https://{domain}/.well-known/webfinger?resource={Uri.EscapeDataString(resource)}";

        try
        {
            _logger.LogInformation("Querying WebFinger at {Url}", webfingerUrl);

            var webfingerResponse = await _httpClient.GetAsync(webfingerUrl);
            if (!webfingerResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("WebFinger query failed with status {StatusCode} for {Handle}", (int)webfingerResponse.StatusCode, handle);
                return null;
            }

            var webfingerJson = await webfingerResponse.Content.ReadFromJsonAsync<JsonNode>();
            if (webfingerJson == null)
                return null;

            var links = webfingerJson["links"]?.AsArray();
            if (links == null)
            {
                _logger.LogWarning("No links found in WebFinger response for {Handle}", handle);
                return null;
            }

            string? actorUrl = null;
            foreach (var link in links)
            {
                var rel = link?["rel"]?.GetValue<string>();
                var type = link?["type"]?.GetValue<string>();
                var href = link?["href"]?.GetValue<string>();

                if (rel == "self" && type == "application/activity+json" && href != null)
                {
                    actorUrl = href;
                    break;
                }
            }

            if (actorUrl == null)
            {
                _logger.LogWarning("No ActivityPub self link found in WebFinger response for {Handle}", handle);
                return null;
            }

            _logger.LogInformation("Resolved {Handle} to actor URL {Url}", handle, actorUrl);
            return await FetchActorAsync(actorUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving actor for handle {Handle}", handle);
            return null;
        }
    }

    private async Task<Actor?> FetchActorAsync(string actorUrl)
    {
        try
        {
            _logger.LogInformation("Fetching actor document from {Url}", actorUrl);
            var response = await _httpClient.GetAsync(actorUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Actor fetch failed with status {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<Actor>(json, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching actor document from {Url}", actorUrl);
            return null;
        }
    }
}
