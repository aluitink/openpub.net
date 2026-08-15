using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Services;

/// <summary>
/// Service for DNS-based federation discovery using SRV records
/// </summary>
public class FederationDiscoveryService : IFederationDiscoveryService
{
    private readonly ILogger<FederationDiscoveryService> _logger;

    public FederationDiscoveryService(ILogger<FederationDiscoveryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers the ActivityPub endpoint for a remote domain using SRV records
    /// </summary>
    /// <param name="domain">The remote domain to discover</param>
    /// <returns>The base URL for ActivityPub operations, or null if discovery fails</returns>
    public async Task<string?> DiscoverEndpointAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain) || string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        try
        {
            // Try SRV record first: _activitypub._tcp.domain
            var srvRecord = ResolveSrvRecord($"_activitypub._tcp.{domain}");
            if (srvRecord != null)
            {
                _logger.LogInformation("Found SRV record for {Domain}: {Target}:{Port}", domain, srvRecord.Target, srvRecord.Port);
                return BuildBaseUrl(srvRecord.Target, srvRecord.Port);
            }

            // Fallback to HTTPS on default port
            _logger.LogInformation("No SRV record found for {Domain}, using default HTTPS", domain);
            return $"https://{domain}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering endpoint for {Domain}", domain);
            return null;
        }
    }

    /// <summary>
    /// Resolves an SRV record for the given service name
    /// </summary>
    /// <param name="serviceName">The SRV record name (e.g., _activitypub._tcp.example.com)</param>
    /// <returns>The SRV record if found, null otherwise</returns>
    private DnsSrvRecord? ResolveSrvRecord(string serviceName)
    {
        try
        {
            var entries = Dns.GetHostEntry(serviceName);
            return null;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
        {
            // SRV record not found
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve SRV record for {Service}", serviceName);
            return null;
        }
    }

    private string BuildBaseUrl(string host, int port)
    {
        return port == 443 ? $"https://{host}" : $"https://{host}:{port}";
    }
}

/// <summary>
/// SRV record response
/// </summary>
public record DnsSrvRecord(string Target, int Port, int Priority, int Weight);

/// <summary>
/// Service interface for federation discovery
/// </summary>
public interface IFederationDiscoveryService
{
    Task<string?> DiscoverEndpointAsync(string domain);
}
