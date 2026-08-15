using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Services;

/// <summary>
/// Service for signing outbound ActivityPub requests with HTTP signatures
/// </summary>
public class OutboundSigningService : IOutboundSigningService
{
    private readonly ILogger<OutboundSigningService> _logger;

    public OutboundSigningService(ILogger<OutboundSigningService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sign an HTTP request with HTTP Signature headers
    /// </summary>
    /// <param name="request">The HTTP request to sign</param>
    /// <param name="privateKeyPem">The private key in PEM format</param>
    /// <param name="keyId">The key ID (usually the actor URL + #main-key)</param>
    /// <param name="hostname">The target hostname</param>
    public void SignRequest(HttpRequestMessage request, string privateKeyPem, string keyId, string hostname)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrEmpty(privateKeyPem)) throw new ArgumentNullException(nameof(privateKeyPem));
        if (string.IsNullOrEmpty(keyId)) throw new ArgumentNullException(nameof(keyId));
        if (string.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));

        try
        {
            // Get the request body if available
            var body = request.Content != null
                ? request.Content.ReadAsStringAsync().Result
                : string.Empty;

            // Define headers to sign (per ActivityPub spec)
            var headersToSign = new List<string> { "(request-target)", "host", "date", "digest" };
            if (!string.IsNullOrEmpty(body))
            {
                headersToSign.Add("digest");
            }

            // Create signature string
            var signatureString = CreateSignatureString(request, headersToSign, hostname);

            // Sign the string
            var signature = SignData(signatureString, privateKeyPem);

            // Add Authorization header
            var authHeader = CreateAuthorizationHeader(keyId, headersToSign, signature);
            request.Headers.Authorization = new AuthenticationHeaderValue("Signature", authHeader);

            // Add Date header if not present
            if (!request.Headers.Date.HasValue)
            {
                request.Headers.Date = DateTime.UtcNow;
            }

            // Add Digest header for body
            if (!string.IsNullOrEmpty(body))
            {
                var digest = ComputeDigest(body);
                request.Headers.Add("Digest", digest);
            }

            // Add Host header
            request.Headers.Host = hostname;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing request for {KeyId}", keyId);
            throw;
        }
    }

    /// <summary>
    /// Creates the signature string from request components
    /// </summary>
    private string CreateSignatureString(HttpRequestMessage request, List<string> headers, string hostname)
    {
        var signatureLines = new List<string>();

        // (request-target) line
        var requestTarget = $"{request.Method.ToString().ToLowerInvariant()} {request.RequestUri?.PathAndQuery}";
        signatureLines.Add($"(request-target): {requestTarget}");

        // Host header
        signatureLines.Add($"host: {hostname}");

        // Date header
        if (request.Headers.Date.HasValue)
        {
            signatureLines.Add($"date: {request.Headers.Date.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        // Digest header (SHA-256 of body)
        if (request.Content != null)
        {
            var body = request.Content.ReadAsStringAsync().Result;
            var digest = ComputeDigest(body);
            signatureLines.Add($"digest: {digest}");
        }

        return string.Join("\n", signatureLines);
    }

    /// <summary>
    /// Signs data using RSA with SHA256
    /// </summary>
    private string SignData(string data, string privateKeyPem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.AsSpan());

        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signature = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// Computes SHA-256 digest of the body
    /// </summary>
    private string ComputeDigest(string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var hash = SHA256.HashData(bodyBytes);
        var digest = Convert.ToBase64String(hash);
        return $"SHA-256={digest}";
    }

    /// <summary>
    /// Creates the Authorization header value
    /// </summary>
    private string CreateAuthorizationHeader(string keyId, List<string> headers, string signature)
    {
        var headerParts = new List<string>
        {
            $"keyId=\"{keyId}\"",
            $"algorithm=\"rsa-sha256\"",
            $"headers=\"{string.Join(" ", headers)}\"",
            $"signature=\"{signature}\""
        };

        return string.Join(", ", headerParts);
    }
}

/// <summary>
/// Interface for outbound signing service
/// </summary>
public interface IOutboundSigningService
{
    void SignRequest(HttpRequestMessage request, string privateKeyPem, string keyId, string hostname);
}
