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

            // Request the ActivityPub self link explicitly. Servers that
            // content-negotiate (and some proxies) return a richer document
            // when they see the AP Accept header, and ignoring it can yield an
            // HTML 200 that we cannot parse.
            using var webfingerRequest = new HttpRequestMessage(HttpMethod.Get, webfingerUrl);
            webfingerRequest.Headers.Accept.ParseAdd("application/activity+json");
            webfingerRequest.Headers.Accept.ParseAdd("application/json");
            webfingerRequest.Headers.Accept.ParseAdd("application/ld+json");

            var webfingerResponse = await _httpClient.SendAsync(webfingerRequest);
            if (!webfingerResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("WebFinger query failed with status {StatusCode} for {Handle}", (int)webfingerResponse.StatusCode, handle);
                return null;
            }

            // Read the body as a string and parse it ourselves. WebFinger
            // responses are frequently served with the JRD media type
            // (application/jrd+json) rather than application/json, and
            // ReadFromJsonAsync<JsonNode>() rejects a 200 whose content type it
            // doesn't recognize — which would silently discard a perfectly valid
            // self link. Parsing the raw body is content-type agnostic.
            var webfingerBody = await webfingerResponse.Content.ReadAsStringAsync();
            JsonNode? webfingerJson;
            try
            {
                webfingerJson = JsonNode.Parse(webfingerBody);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "WebFinger response for {Handle} was not parseable JSON", handle);
                return null;
            }
            if (webfingerJson == null)
                return null;

            var links = webfingerJson["links"]?.AsArray();
            if (links == null)
            {
                _logger.LogWarning("No links found in WebFinger response for {Handle}", handle);
                return null;
            }

            // Prefer a self link typed as the ActivityPub media type, but fall
            // back to any `rel == "self"` link: several ActivityPub stacks
            // omit `type` on the self link, and requiring the media type makes
            // resolution fail against perfectly valid servers.
            string? preferredUrl = null;
            string? anySelfUrl = null;
            foreach (var link in links)
            {
                var rel = link?["rel"]?.GetValue<string>();
                var type = link?["type"]?.GetValue<string>();
                var href = link?["href"]?.GetValue<string>();
                if (rel != "self" || href == null)
                    continue;

                if (type == "application/activity+json")
                {
                    preferredUrl = href;
                    break;
                }
                anySelfUrl ??= href;
            }

            var actorUrl = preferredUrl ?? anySelfUrl;
            if (actorUrl == null)
            {
                _logger.LogWarning("No self link found in WebFinger response for {Handle}", handle);
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
            using var actorRequest = new HttpRequestMessage(HttpMethod.Get, actorUrl);
            actorRequest.Headers.Accept.ParseAdd("application/activity+json");
            actorRequest.Headers.Accept.ParseAdd("application/ld+json");
            actorRequest.Headers.Accept.ParseAdd("application/json");
            var response = await _httpClient.SendAsync(actorRequest);
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
