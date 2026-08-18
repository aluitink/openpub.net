using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Services;

/// <summary>
/// Service for sending outbound ActivityPub activities to remote servers
/// </summary>
public class OutboundActivityService : IOutboundActivityService
{
    private readonly HttpClient _httpClient;
    private readonly IFederationDiscoveryService _federationDiscovery;
    private readonly IOutboundSigningService _signingService;
    private readonly ILogger<OutboundActivityService> _logger;

    public OutboundActivityService(
        HttpClient httpClient,
        IFederationDiscoveryService federationDiscovery,
        IOutboundSigningService signingService,
        ILogger<OutboundActivityService> logger)
    {
        _httpClient = httpClient;
        _federationDiscovery = federationDiscovery;
        _signingService = signingService;
        _logger = logger;
    }

    /// <summary>
    /// Sends an activity to a remote server
    /// </summary>
    /// <param name="activity">The activity to send</param>
    /// <param name="actorId">The ID of the sending actor</param>
    /// <param name="privateKeyPem">The private key for signing</param>
    /// <param name="to">The recipient URL (inbox or shared inbox)</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> SendActivityAsync(string activity, string actorId, string privateKeyPem, string to)
    {
        if (string.IsNullOrEmpty(activity)) throw new ArgumentNullException(nameof(activity));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentNullException(nameof(actorId));
        if (string.IsNullOrEmpty(privateKeyPem)) throw new ArgumentNullException(nameof(privateKeyPem));
        if (string.IsNullOrEmpty(to)) throw new ArgumentNullException(nameof(to));

        try
        {
            // The `to` argument is the recipient's *actual* delivery endpoint —
            // the actor's `inbox` or `endpoints.sharedInbox`, resolved up front
            // from the remote actor's JSON-LD (see WebFingerService + the
            // Follow/queue flow). Honor it verbatim rather than re-deriving a
            // path: re-deriving `{domain}/inbox` only coincides with the real
            // inbox on stock Mastodon and 404s on Pleroma/Akkoma subpath,
            // per-user-inbox, or custom deployments.
            var inboxUrl = to;
            string domain;

            // Honor the resolved inbox/sharedInbox verbatim. Fall back to SRV
            // discovery only when `to` is not an absolute http(s) URL
            // (defensive; callers always pass an absolute inbox).
            if (inboxUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                inboxUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                domain = new Uri(inboxUrl).Host;
            }
            else
            {
                domain = new Uri(to).Host;
                var endpoint = await _federationDiscovery.DiscoverEndpointAsync(domain);
                if (string.IsNullOrEmpty(endpoint))
                {
                    _logger.LogWarning("Failed to discover endpoint for {Domain}", domain);
                    return false;
                }
                inboxUrl = BuildInboxUrl(endpoint, domain);
            }

            // Create HTTP request
            var request = new HttpRequestMessage(HttpMethod.Post, inboxUrl);
            request.Content = new StringContent(activity, Encoding.UTF8, "application/activity+json");

            // Sign the request. The host for the signature is the resolved
            // inbox's host, which can differ from the raw `to` host when a
            // shared-inbox lives on a different host than the actor document.
            var keyId = $"{actorId}#main-key";
            _signingService.SignRequest(request, privateKeyPem, keyId, domain);

            // Send request
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent activity to {InboxUrl}", inboxUrl);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to send activity to {InboxUrl}: {StatusCode}", inboxUrl, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending activity to {To}", to);
            return false;
        }
    }

    /// <summary>
    /// Sends an activity to a shared inbox
    /// </summary>
    /// <param name="activity">The activity to send</param>
    /// <param name="actorId">The ID of the sending actor</param>
    /// <param name="privateKeyPem">The private key for signing</param>
    /// <param name="sharedInboxUrl">The shared inbox URL</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> SendToSharedInboxAsync(string activity, string actorId, string privateKeyPem, string sharedInboxUrl)
    {
        return await SendActivityAsync(activity, actorId, privateKeyPem, sharedInboxUrl);
    }

    /// <summary>
    /// Builds the inbox URL from endpoint and domain.
    /// </summary>
    private string BuildInboxUrl(string endpoint, string domain)
    {
        // Most ActivityPub servers expose a shared inbox at /inbox or use the
        // server's base path. We prefer the server's shared inbox (which is
        // what the remote actor's JSON-LD 'endpoints.sharedInbox' would point
        // to) and fall back to /inbox. We do NOT probe with HEAD requests
        // (the previous implementation did, with a sync-over-async .Result
        // call that risked deadlocks, and it treated 404 as "valid" so it
        // could pick a non-existent path).
        return $"{endpoint}/inbox";
    }
}

/// <summary>
/// Interface for outbound activity service
/// </summary>
public interface IOutboundActivityService
{
    Task<bool> SendActivityAsync(string activity, string actorId, string privateKeyPem, string to);
    Task<bool> SendToSharedInboxAsync(string activity, string actorId, string privateKeyPem, string sharedInboxUrl);
}
