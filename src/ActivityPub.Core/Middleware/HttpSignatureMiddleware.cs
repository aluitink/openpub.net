using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;

namespace ActivityPub.Core.Middleware;

/// <summary>
/// Middleware for verifying HTTP signatures according to W3C draft-cavage-http-signatures-12 spec
/// </summary>
public class HttpSignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpSignatureMiddleware> _logger;
    private static readonly HashSet<string> _allowedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "digest",
        "created",
        "expires"
    };

    public HttpSignatureMiddleware(RequestDelegate next, ILogger<HttpSignatureMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only process POST requests to inbox endpoints
        if (context.Request.Method == "POST" && context.Request.Path.StartsWithSegments("/users"))
        {
            try
            {
                if (IsInboxPath(context.Request.Path))
                {
                    if (!await VerifyHttpSignatureAsync(context))
                    {
                        if (context.Response.StatusCode < 400)
                        {
                            context.Response.StatusCode = 401;
                        }
                        _logger.LogWarning("HTTP signature verification failed for request to {Path}", context.Request.Path);
                        await WriteResponseAsync(context, "Unauthorized: Invalid HTTP signature");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing HTTP signature for request to {Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                await WriteResponseAsync(context, "Unauthorized: Error processing signature");
                return;
            }
        }

        await _next(context);
    }

    private bool IsInboxPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments?.Length >= 3 && segments[segments.Length - 1] == "inbox";
    }

    private async Task<bool> VerifyHttpSignatureAsync(HttpContext context)
    {
        var signatureHeader = context.Request.Headers["Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signatureHeader))
        {
            signatureHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("Signature ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            signatureHeader = signatureHeader.Substring(10);
        }

        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        try
        {
            var signatureParams = ParseSignatureHeader(signatureHeader);

            if (!signatureParams.ContainsKey("keyId") || !signatureParams.ContainsKey("signature"))
            {
                return false;
            }

            // Check replay protection (created/expired) before fetching public key
            // This ensures expired signatures are rejected with 403 even if key fetch fails
            if (!ValidateReplayProtection(context, signatureParams))
            {
                context.Response.StatusCode = 403;
                return false;
            }

            var publicKey = await GetPublicKeyForVerificationAsync(context, signatureParams["keyId"]);
            if (publicKey == null)
            {
                return false;
            }

            var signedContent = await CreateSignedContentAsync(context, signatureParams);
            if (string.IsNullOrEmpty(signedContent))
            {
                return false;
            }

            if (!VerifySignature(signedContent, signatureParams["signature"], publicKey.PublicKeyPem))
            {
                context.Response.StatusCode = 401;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying HTTP signature");
            return false;
        }
    }

    private bool ValidateReplayProtection(HttpContext context, Dictionary<string, string> signatureParams)
    {
        const int maxTimeSkewSeconds = 300;
        bool hasCreated = false;
        bool hasExpires = false;
        long created = 0;
        long expires = 0;

        // Check signature params first (created=, expires= in Signature header)
        if (signatureParams.TryGetValue("created", out var createdStr) && long.TryParse(createdStr, out created))
        {
            hasCreated = true;
        }

        if (signatureParams.TryGetValue("expires", out var expiresStr) && long.TryParse(expiresStr, out expires))
        {
            hasExpires = true;
        }

        // If not in signature params, check HTTP headers ((created), (expires))
        if (!hasCreated && context.Request.Headers.TryGetValue("(created)", out StringValues createdHeader) &&
            long.TryParse(createdHeader.FirstOrDefault(), out created))
        {
            hasCreated = true;
        }

        if (!hasExpires && context.Request.Headers.TryGetValue("(expires)", out StringValues expiresHeader) &&
            long.TryParse(expiresHeader.FirstOrDefault(), out expires))
        {
            hasExpires = true;
        }

        if (!hasCreated)
        {
            _logger.LogWarning("Signature missing required 'created' field for replay attack protection");
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (created < now - maxTimeSkewSeconds || created > now + maxTimeSkewSeconds)
        {
            _logger.LogWarning("Signature created time is outside acceptable window");
            return false;
        }

        if (hasExpires)
        {
            if (expires <= now)
            {
                _logger.LogWarning("Signature has expired");
                return false;
            }

            if (expires < created)
            {
                _logger.LogWarning("Signature expires before it is created");
                return false;
            }
        }

        return true;
    }

    private async Task WriteResponseAsync(HttpContext context, string message)
    {
        if (context.Response.Body.CanWrite)
        {
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(message);
        }
    }

    private Dictionary<string, string> ParseSignatureHeader(string signatureHeader)
    {
        var paramsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pairs = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (keyValue.Length == 2)
            {
                var value = keyValue[1].Trim('"');
                paramsDict[keyValue[0]] = value;
            }
        }

        return paramsDict;
    }

    private async Task<PublicKey?> GetPublicKeyForVerificationAsync(HttpContext context, string keyId)
    {
        var keyFetchingService = context.RequestServices.GetService<IKeyFetchingService>();
        if (keyFetchingService != null)
        {
            var publicKey = await keyFetchingService.FetchPublicKeyAsync(keyId);
            return publicKey;
        }

        return null;
    }

    private async Task<string> CreateSignedContentAsync(HttpContext context, Dictionary<string, string> signatureParams)
    {
        var headersToSign = GetHeadersToSign(signatureParams);
        var builder = new StringBuilder();

        for (int i = 0; i < headersToSign.Count; i++)
        {
            var headerName = headersToSign[i];
            var headerValue = GetHeaderValue(context, headerName);

            if (string.IsNullOrEmpty(headerValue))
            {
                headerValue = string.Empty;
            }

            builder.Append(headerName).Append(": ").Append(headerValue);

            if (i < headersToSign.Count - 1)
            {
                builder.Append("\n");
            }
        }

        return builder.ToString();
    }

    private List<string> GetHeadersToSign(Dictionary<string, string> signatureParams)
    {
        if (signatureParams.TryGetValue("headers", out var headersStr))
        {
            var headers = headersStr.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return headers.Where(h => !string.IsNullOrEmpty(h)).ToList();
        }

        return new List<string> { "request-target" };
    }

    private string GetHeaderValue(HttpContext context, string headerName)
    {
        headerName = headerName.ToLowerInvariant();

        switch (headerName)
        {
            case "request-target":
                var methodName = context.Request.Method?.ToUpperInvariant() ?? "GET";
                var path = context.Request.Path.Value;
                var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
                return $"{methodName.ToLower()} {path}{queryString}";

            case "digest":
                if (context.Request.Headers.TryGetValue("Digest", out StringValues digestValues))
                {
                    return digestValues.FirstOrDefault() ?? string.Empty;
                }
                return string.Empty;

            case "created":
            case "expires":
                return context.Request.Headers.TryGetValue(headerName, out StringValues values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;

            default:
                if (context.Request.Headers.TryGetValue(headerName, out StringValues headerValues))
                {
                    return headerValues.FirstOrDefault() ?? string.Empty;
                }
                return string.Empty;
        }
    }

    private bool VerifySignature(string content, string signature, string publicKeyPem)
    {
        try
        {
            var signatureBytes = Convert.FromBase64String(signature);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            var hashAlgorithm = SHA256.Create();
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var hash = hashAlgorithm.ComputeHash(contentBytes);

            // Verify using the hash (signature was computed on the hash)
            return rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Signature verification failed");
            return false;
        }
    }
}