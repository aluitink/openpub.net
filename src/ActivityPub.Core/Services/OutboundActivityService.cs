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
            // Extract domain from recipient URL
            var recipientUri = new Uri(to);
            var domain = recipientUri.Host;

            // Discover endpoint via DNS SRV record
            var endpoint = await _federationDiscovery.DiscoverEndpointAsync(domain);
            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogWarning("Failed to discover endpoint for {Domain}", domain);
                return false;
            }

            // Construct the inbox URL
            var inboxUrl = BuildInboxUrl(endpoint, domain);
            if (string.IsNullOrEmpty(inboxUrl))
            {
                _logger.LogWarning("Failed to build inbox URL for {Domain}", domain);
                return false;
            }

            // Create HTTP request
            var request = new HttpRequestMessage(HttpMethod.Post, inboxUrl);
            request.Content = new StringContent(activity, Encoding.UTF8, "application/activity+json");

            // Sign the request
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
    /// Builds the inbox URL from endpoint and domain
    /// </summary>
    private string BuildInboxUrl(string endpoint, string domain)
    {
        // Try common inbox paths
        var paths = new[] { $"/users/{domain}/inbox", $"/inbox", $"/users/{domain}/outbox" };
        
        foreach (var path in paths)
        {
            var url = $"{endpoint}{path}";
            if (TestInboxUrl(url).Result)
            {
                return url;
            }
        }

        // Fallback to default
        return $"{endpoint}/inbox";
    }

    /// <summary>
    /// Tests if an inbox URL is reachable
    /// </summary>
    private async Task<bool> TestInboxUrl(string url)
    {
        try
        {
            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
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
