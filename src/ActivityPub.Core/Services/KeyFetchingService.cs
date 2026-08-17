using System.Text.Json;
using System.Text.Json.Nodes;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Services;

/// <summary>
/// Service for fetching public keys from remote servers via Actor documents
/// </summary>
public class KeyFetchingService : IKeyFetchingService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KeyFetchingService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public KeyFetchingService(HttpClient httpClient, IMemoryCache cache, ILogger<KeyFetchingService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Fetches a public key from a remote server using the Actor document
    /// </summary>
    /// <param name="keyId">The URI of the public key</param>
    /// <returns>The public key if found, null otherwise</returns>
    public async Task<PublicKey?> FetchPublicKeyAsync(string keyId)
    {
        if (string.IsNullOrEmpty(keyId))
        {
            return null;
        }

        if (_cache.TryGetValue(keyId, out PublicKey? cachedKey))
        {
            return cachedKey;
        }

        try
        {
            var actorUri = ExtractActorUriFromKeyId(keyId);
            if (string.IsNullOrEmpty(actorUri))
            {
                return null;
            }

            // The keyId's base URL is itself the actor's JSON-LD document
            // (e.g. https://host/users/alice#main-key -> https://host/users/alice).
            // Fetch it directly with an ActivityPub accept header.
            using var request = new HttpRequestMessage(HttpMethod.Get, actorUri);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/activity+json"));
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/ld+json"));

            HttpResponseMessage actorResponse;
            try
            {
                actorResponse = await _httpClient.SendAsync(request);
            }
            catch (HttpRequestException)
            {
                return null;
            }

            if (!actorResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var actorContent = await actorResponse.Content.ReadAsStringAsync();
            var actorData = JsonNode.Parse(actorContent);

            if (actorData == null)
            {
                return null;
            }

            var publicKey = ExtractPublicKeyFromActorDocument(actorData, keyId);
            if (publicKey != null)
            {
                _cache.Set(keyId, publicKey, TimeSpan.FromHours(1));
                return publicKey;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching public key for {KeyId}", keyId);
        }

        return null;
    }

    private string ExtractActorUriFromKeyId(string keyId)
    {
        var fragmentIndex = keyId.IndexOf('#');
        if (fragmentIndex > 0)
        {
            return keyId.Substring(0, fragmentIndex);
        }
        return keyId;
    }

    private PublicKey? ExtractPublicKeyFromActorDocument(JsonNode actorData, string keyId)
    {
        if (actorData is not JsonObject actorObj)
        {
            return null;
        }

        var publicKeyObj = actorObj["publicKey"];
        if (publicKeyObj == null)
        {
            return null;
        }

        try
        {
            var publicKeyJson = publicKeyObj.ToJsonString(_jsonOptions);
            var publicKey = JsonSerializer.Deserialize<PublicKey>(publicKeyJson, _jsonOptions);
            return publicKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing public key for {KeyId}", keyId);
            return null;
        }
    }
}
