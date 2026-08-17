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
            // Get the request body if available. The content must be read
            // synchronously because SignRequest is a sync method invoked just
            // before the request is sent; the body is small (a single
            // ActivityStreams JSON document) so this is acceptable.
            var body = request.Content != null
                ? request.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                : string.Empty;

            // Ensure the headers we are about to cover actually exist on the
            // request *before* we build the signature string, so the signed
            // content and the 'headers' parameter stay in agreement.
            if (!request.Headers.Date.HasValue)
            {
                request.Headers.Date = DateTime.UtcNow;
            }

            // Add the Digest header (SHA-256 of the body) when there is a body.
            if (!string.IsNullOrEmpty(body) && !request.Headers.TryGetValues("Digest", out _))
            {
                request.Headers.Add("Digest", ComputeDigest(body));
            }

            // Add Host header
            request.Headers.Host = hostname;

            // The 'created' timestamp (Unix epoch seconds) is a required
            // parameter in the W3C HTTP Signature draft and is expected by
            // Mastodon and most other ActivityPub servers. It is not covered
            // as a header component; it is carried as a signature parameter.
            var created = (long)(request.Headers.Date?.ToUniversalTime() ?? DateTime.UtcNow).Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            // Covered components, in the exact order they appear in the signed
            // string. 'digest' is only covered when a body is present.
            var headersToSign = new List<string> { "(request-target)", "host", "date" };
            if (!string.IsNullOrEmpty(body))
            {
                headersToSign.Add("digest");
            }

            // Create signature string
            var signatureString = CreateSignatureString(request, headersToSign, hostname);

            // Sign the string
            var signature = SignData(signatureString, privateKeyPem);

            // Add Authorization header (with the 'created' signature parameter)
            var authHeader = CreateAuthorizationHeader(keyId, headersToSign, signature, created);
            request.Headers.Authorization = new AuthenticationHeaderValue("Signature", authHeader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing request for {KeyId}", keyId);
            throw;
        }
    }

    /// <summary>
    /// Creates the signature string from the covered components, in the exact
    /// order they appear in <paramref name="headers"/>. Each line is
    /// <c>name: value</c>, joined by newlines — matching the W3C
    /// draft-cavage-http-signatures construction and the verifier.
    /// </summary>
    private string CreateSignatureString(HttpRequestMessage request, List<string> headers, string hostname)
    {
        var signatureLines = new List<string>();

        foreach (var header in headers)
        {
            var value = header switch
            {
                "(request-target)" =>
                    $"{request.Method.ToString().ToLowerInvariant()} {request.RequestUri?.PathAndQuery}",
                "host" => hostname,
                "date" => request.Headers.Date?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                "digest" => request.Headers.TryGetValues("Digest", out var digestValues)
                    ? digestValues.FirstOrDefault() ?? string.Empty
                    : string.Empty,
                _ => string.Empty,
            };
            signatureLines.Add($"{header}: {value}");
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
    private string CreateAuthorizationHeader(string keyId, List<string> headers, string signature, long created)
    {
        var headerParts = new List<string>
        {
            $"keyId=\"{keyId}\"",
            $"algorithm=\"rsa-sha256\"",
            $"headers=\"{string.Join(" ", headers)}\"",
            $"signature=\"{signature}\"",
            $"created={created}"
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
