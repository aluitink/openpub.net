using System.Text.Json;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace ActivityPub.Core.Services;

/// <summary>
/// Service for fetching public keys from remote servers via Actor documents
/// </summary>
public class KeyFetchingService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public KeyFetchingService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    /// <summary>
    /// Fetches a public key from a remote server using the Actor document
    /// </summary>
    /// <param name="keyId">The URI of the public key</param>
    /// <returns>The public key if found, null otherwise</returns>
    public async Task<PublicKey?> FetchPublicKeyAsync(string keyId)
    {
        // Check cache first
        if (_cache.TryGetValue(keyId, out PublicKey? cachedKey))
        {
            return cachedKey;
        }

        try
        {
            // Extract the actor URI from the key ID (assumes keyId looks like: https://domain.tld/users/username#key-id)
            var actorUri = ExtractActorUriFromKeyId(keyId);
            if (string.IsNullOrEmpty(actorUri))
            {
                return null;
            }

            // Fetch the actor document directly
            var actorUrl = $"{actorUri}.jsonld"; // ActivityPub expects .jsonld for actor documents
            
            // Try the .jsonld endpoint first, fallback to .json
            var actorResponse = await _httpClient.GetAsync(actorUrl);
            if (!actorResponse.IsSuccessStatusCode)
            {
                // Try alternative endpoint
                actorUrl = $"{actorUri}.json";
                actorResponse = await _httpClient.GetAsync(actorUrl);
            }
            
            if (actorResponse.IsSuccessStatusCode)
            {
                var actorContent = await actorResponse.Content.ReadAsStringAsync();
                var actorData = JsonSerializer.Deserialize<Dictionary<string, object>>(actorContent);
                
                if (actorData != null)
                {
                    // Extract public key from actor document
                    var publicKey = ExtractPublicKeyFromActorDocument(actorData, keyId);
                    if (publicKey != null)
                    {
                        // Cache for future use
                        _cache.Set(keyId, publicKey, TimeSpan.FromHours(1));
                        return publicKey;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue
        }

        return null;
    }

    private string ExtractActorUriFromKeyId(string keyId)
    {
        // Extract the actor URI from a key ID (should be in format: https://domain.tld/users/username#key-id)
        var fragmentIndex = keyId.IndexOf('#');
        if (fragmentIndex > 0)
        {
            return keyId.Substring(0, fragmentIndex);
        }
        return keyId;
    }

    private PublicKey? ExtractPublicKeyFromActorDocument(Dictionary<string, object> actorData, string keyId)
    {
        // Look for publicKey in the actor document
        if (actorData.TryGetValue("publicKey", out var publicKeyObj))
        {
            // This is a simplified implementation - in production you'd parse the full public key structure
            var publicKeyJson = JsonSerializer.Serialize(publicKeyObj);
            return JsonSerializer.Deserialize<PublicKey>(publicKeyJson);
        }
        
        return null;
    }
}