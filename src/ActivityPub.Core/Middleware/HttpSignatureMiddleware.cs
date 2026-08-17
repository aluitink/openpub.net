using System.Security.Cryptography;
using System.Text;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ActivityPub.Core.Middleware;

/// <summary>
/// Middleware for verifying HTTP signatures on incoming ActivityPub activity
/// deliveries, per the W3C draft-cavage-http-signatures spec.
///
/// Verification posture (driven by <see cref="ActivityPubOptions"/>):
///   * <c>EnableSignatureVerification == false</c>: verification is skipped entirely
///     (pass-through). This is the "federation off" posture.
///   * <c>EnableSignatureVerification == true, RequireSignatures == false</c>:
///     a present signature is verified and the request is rejected if it is
///     invalid; an absent signature is tolerated (logged). This preserves local
///     development / testing where posts are unsigned while still rejecting
///     tampered or forged signed deliveries.
///   * <c>EnableSignatureVerification == true, RequireSignatures == true</c>:
///     every inbox delivery must carry a valid signature; unsigned requests are
///     rejected with 401. This is the full production federation posture.
/// </summary>
public class HttpSignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpSignatureMiddleware> _logger;
    private readonly ActivityPubOptions _options;

    public HttpSignatureMiddleware(
        RequestDelegate next,
        ILogger<HttpSignatureMiddleware> logger,
        IOptions<ActivityPubOptions>? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options?.Value ?? new ActivityPubOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only enforce on inbound activity deliveries: POST to an inbox endpoint.
        if (context.Request.Method == "POST" && IsInboxPath(context.Request.Path))
        {
            // Verification disabled: pass through (federation off posture).
            if (!_options.EnableSignatureVerification)
            {
                await _next(context);
                return;
            }

            // No signature present.
            var signatureHeader = GetRawSignatureHeader(context);
            if (string.IsNullOrEmpty(signatureHeader))
            {
                if (_options.RequireSignatures)
                {
                    _logger.LogWarning(
                        "Rejecting unsigned inbox delivery to {Path} (RequireSignatures is enabled)",
                        context.Request.Path);
                    await RejectAsync(context, 401, "Unauthorized: HTTP signature is required");
                    return;
                }

                // Tolerate unsigned (local dev / testing) but log it.
                _logger.LogWarning(
                    "Inbox delivery to {Path} had no HTTP signature; accepting (RequireSignatures is disabled)",
                    context.Request.Path);
                await _next(context);
                return;
            }

            try
            {
                var (status, message) = await VerifyHttpSignatureAsync(context, signatureHeader);
                if (status != 200)
                {
                    _logger.LogWarning(
                        "HTTP signature verification failed for {Path}: {Message}",
                        context.Request.Path, message);
                    await RejectAsync(context, status, $"Unauthorized: {message}");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing HTTP signature for request to {Path}", context.Request.Path);
                await RejectAsync(context, 401, "Unauthorized: Error processing signature");
                return;
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Returns true when the path is an ActivityPub inbox endpoint
    /// (e.g. /users/{name}/inbox or /inbox or /shared-inbox).
    /// </summary>
    private static bool IsInboxPath(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value is "/inbox" or "/shared-inbox")
        {
            return true;
        }

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3 && segments[^1] == "inbox";
    }

    /// <summary>
    /// Reads the signature from the dedicated <c>Signature</c> header, or from
    /// an <c>Authorization: Signature ...</c> header (the form our outbound
    /// signer produces). Returns the parameter string (without the "Signature"
    /// scheme prefix) or null when absent.
    /// </summary>
    private static string? GetRawSignatureHeader(HttpContext context)
    {
        var signatureHeader = context.Request.Headers["Signature"].FirstOrDefault();
        if (!string.IsNullOrEmpty(signatureHeader))
        {
            return signatureHeader;
        }

        var authorization = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authorization) &&
            authorization.StartsWith("Signature ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization.Substring("Signature ".Length);
        }

        return null;
    }

    private async Task<(int Status, string Message)> VerifyHttpSignatureAsync(HttpContext context, string signatureHeader)
    {
        var signatureParams = ParseSignatureHeader(signatureHeader);

        if (!signatureParams.ContainsKey("keyId") || !signatureParams.ContainsKey("signature"))
        {
            return (401, "Signature header is missing keyId or signature");
        }

        // Replay protection must hold before we trust anything else.
        if (!ValidateReplayProtection(context, signatureParams, out var replayMessage))
        {
            return (403, replayMessage);
        }

        var publicKey = await GetPublicKeyForVerificationAsync(context, signatureParams["keyId"]);
        if (publicKey == null || string.IsNullOrEmpty(publicKey.PublicKeyPem))
        {
            return (401, $"Could not resolve a public key for keyId '{signatureParams["keyId"]}'");
        }

        var signedContent = await CreateSignedContentAsync(context, signatureParams);
        if (signedContent == null)
        {
            return (401, "Could not reconstruct the signed content");
        }

        if (!VerifySignature(signedContent, signatureParams["signature"], publicKey.PublicKeyPem))
        {
            return (401, "Signature does not match");
        }

        // When a digest was signed, confirm the body actually matches it so a
        // forged body cannot ride on a valid signature.
        if (signatureParams.ContainsKey("headers") &&
            signatureParams["headers"].Contains("digest", StringComparison.OrdinalIgnoreCase))
        {
            if (!await ValidateDigestAsync(context))
            {
                return (401, "Body does not match the signed digest");
            }
        }

        return (200, "ok");
    }

    private bool ValidateReplayProtection(HttpContext context, Dictionary<string, string> signatureParams, out string message)
    {
        const long maxTimeSkewSeconds = 300;
        long created;

        if (TryGetTimestamp(signatureParams, "created", context, out created))
        {
            // found
        }
        else
        {
            message = "Signature is missing the 'created' timestamp required for replay protection";
            return false;
        }

        var hasExpires = TryGetTimestamp(signatureParams, "expires", context, out var expires);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (created < now - maxTimeSkewSeconds || created > now + maxTimeSkewSeconds)
        {
            message = "Signature 'created' timestamp is outside the acceptable window";
            return false;
        }

        if (hasExpires)
        {
            if (expires <= now)
            {
                message = "Signature has expired";
                return false;
            }

            if (expires < created)
            {
                message = "Signature expires before it was created";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Reads a numeric timestamp from a signature parameter (preferred) or an
    /// HTTP header of the same name. Returns false when neither is present or
    /// parseable.
    /// </summary>
    private static bool TryGetTimestamp(
        Dictionary<string, string> signatureParams,
        string name,
        HttpContext context,
        out long value)
    {
        value = 0;

        if (signatureParams.TryGetValue(name, out var paramValue) && long.TryParse(paramValue, out var parsed))
        {
            value = parsed;
            return true;
        }

        if (context.Request.Headers.TryGetValue(name, out var headerValues) &&
            long.TryParse(headerValues.FirstOrDefault(), out var parsedHeader))
        {
            value = parsedHeader;
            return true;
        }

        return false;
    }

    private static async Task RejectAsync(HttpContext context, int statusCode, string message)
    {
        if (!context.Response.HasStarted && context.Response.Body.CanWrite)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(message);
        }
    }

    private static Dictionary<string, string> ParseSignatureHeader(string signatureHeader)
    {
        var paramsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // The signature value is base64 and can contain characters we must not
        // split on; parse key=value pairs where values are double-quoted.
        foreach (var (key, value) in ParseParameters(signatureHeader))
        {
            paramsDict[key] = value;
        }

        return paramsDict;
    }

    /// <summary>
    /// Parses a signature parameter string of the form
    /// <c>keyId="...",headers="a b c",signature="...",created="123"</c> into
    /// key/value pairs, correctly handling quoted values that contain commas
    /// or other characters.
    /// </summary>
    private static IEnumerable<(string Key, string Value)> ParseParameters(string input)
    {
        var i = 0;
        while (i < input.Length)
        {
            // Skip whitespace and commas between parameters.
            while (i < input.Length && (input[i] == ',' || char.IsWhiteSpace(input[i])))
            {
                i++;
            }

            if (i >= input.Length)
            {
                break;
            }

            // Read the key up to '='.
            var keyStart = i;
            while (i < input.Length && input[i] != '=')
            {
                i++;
            }

            if (i >= input.Length || input[i] != '=')
            {
                break;
            }

            var key = input[keyStart..i].Trim();
            i++; // consume '='

            // Read the value: quoted or bare token.
            string value;
            if (i < input.Length && input[i] == '"')
            {
                i++; // consume opening quote
                var valueStart = i;
                while (i < input.Length && input[i] != '"')
                {
                    i++;
                }
                value = input[valueStart..i];
                if (i < input.Length)
                {
                    i++; // consume closing quote
                }
            }
            else
            {
                var valueStart = i;
                while (i < input.Length && input[i] != ',' && !char.IsWhiteSpace(input[i]))
                {
                    i++;
                }
                value = input[valueStart..i];
            }

            if (key.Length > 0)
            {
                yield return (key, value);
            }
        }
    }

    private async Task<PublicKey?> GetPublicKeyForVerificationAsync(HttpContext context, string keyId)
    {
        var keyFetchingService = context.RequestServices.GetService<IKeyFetchingService>();
        if (keyFetchingService != null)
        {
            return await keyFetchingService.FetchPublicKeyAsync(keyId);
        }

        return null;
    }

    private async Task<string?> CreateSignedContentAsync(HttpContext context, Dictionary<string, string> signatureParams)
    {
        var headersToSign = GetHeadersToSign(signatureParams);
        if (headersToSign.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < headersToSign.Count; i++)
        {
            var headerName = headersToSign[i];
            var headerValue = GetHeaderValue(context, headerName);
            builder.Append(NormalizeHeaderName(headerName)).Append(": ").Append(headerValue);
            if (i < headersToSign.Count - 1)
            {
                builder.Append('\n');
            }
        }

        // Ensure the request body is fully buffered so a later digest check and
        // controller binding can both read it.
        if (signatureParams.ContainsKey("headers") &&
            signatureParams["headers"].Contains("digest", StringComparison.OrdinalIgnoreCase))
        {
            await BufferRequestBodyAsync(context);
        }

        return builder.ToString();
    }

    private static async Task BufferRequestBodyAsync(HttpContext context)
    {
        // Read the body fully into memory so it can be re-read by both this
        // middleware (digest validation) and the downstream model binder. The
        // resulting stream is intentionally NOT disposed here — it is now the
        // request body.
        var memoryStream = new MemoryStream();
        await context.Request.Body.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        context.Request.Body = memoryStream;
    }

    private static List<string> GetHeadersToSign(Dictionary<string, string> signatureParams)
    {
        if (signatureParams.TryGetValue("headers", out var headersStr) && !string.IsNullOrWhiteSpace(headersStr))
        {
            return headersStr
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(h => !string.IsNullOrEmpty(h))
                .ToList();
        }

        // Per spec, when no covered components are listed, only
        // (request-target) is covered.
        return new List<string> { "(request-target)" };
    }

    /// <summary>
    /// Normalizes a covered-component name to its canonical signed-name form:
    /// <c>(request-target)</c> keeps its parentheses, other names are
    /// lower-cased and any surrounding parentheses are stripped
    /// (e.g. <c>(host)</c> -> <c>host</c>).
    /// </summary>
    private static string NormalizeHeaderName(string headerName)
    {
        var trimmed = headerName.Trim();
        if (trimmed.Equals("(request-target)", StringComparison.OrdinalIgnoreCase))
        {
            return "(request-target)";
        }

        var start = trimmed.StartsWith('(') ? 1 : 0;
        var end = trimmed.EndsWith(')') ? trimmed.Length - 1 : trimmed.Length;
        var name = trimmed[start..end];
        return name.Length == 0 ? trimmed.ToLowerInvariant() : name.ToLowerInvariant();
    }

    private string GetHeaderValue(HttpContext context, string headerName)
    {
        var normalized = NormalizeHeaderName(headerName);

        switch (normalized)
        {
            case "(request-target)":
                var method = (context.Request.Method ?? "GET").ToLowerInvariant();
                var path = context.Request.Path.Value ?? string.Empty;
                var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
                return $"{method} {path}{query}";

            case "digest":
                return context.Request.Headers.TryGetValue("Digest", out var digestValues)
                    ? digestValues.FirstOrDefault() ?? string.Empty
                    : string.Empty;

            default:
                // 'created'/'expires' and ordinary headers are read by name.
                if (context.Request.Headers.TryGetValue(normalized, out var values))
                {
                    return values.FirstOrDefault() ?? string.Empty;
                }

                return string.Empty;
        }
    }

    /// <summary>
    /// Confirms the request body matches the <c>SHA-256=...</c> digest header.
    /// </summary>
    private async Task<bool> ValidateDigestAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Digest", out var digestValues))
        {
            return false;
        }

        var digestHeader = digestValues.FirstOrDefault();
        if (string.IsNullOrEmpty(digestHeader) ||
            !digestHeader.StartsWith("SHA-256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedDigest = digestHeader["SHA-256=".Length..];
        var bodyBytes = await ReadBodyAsync(context);
        var actualDigest = Convert.ToBase64String(SHA256.HashData(bodyBytes));
        return string.Equals(expectedDigest, actualDigest, StringComparison.Ordinal);
    }

    private async Task<byte[]> ReadBodyAsync(HttpContext context)
    {
        var stream = context.Request.Body;
        if (!stream.CanSeek)
        {
            return Array.Empty<byte>();
        }

        var position = stream.Position;
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        stream.Position = position;
        return memoryStream.ToArray();
    }

    private bool VerifySignature(string content, string signature, string publicKeyPem)
    {
        try
        {
            var signatureBytes = Convert.FromBase64String(signature);
            var contentBytes = Encoding.UTF8.GetBytes(content);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            // The outbound signer (and standard ActivityPub implementations)
            // sign the raw signed-content string with RSA-SHA256/PKCS#1, so the
            // verifier must verify the same raw bytes — not a pre-computed hash.
            return rsa.VerifyData(contentBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Signature verification failed");
            return false;
        }
    }
}
