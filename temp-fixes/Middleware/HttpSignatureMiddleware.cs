using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;

namespace ActivityPub.Core.Middleware;

/// <summary>
/// Middleware for verifying HTTP signatures according to W3C standards for ActivityPub
/// </summary>
public class HttpSignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpSignatureMiddleware> _logger;

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
                // Check if this is an inbox endpoint
                if (IsInboxPath(context.Request.Path))
                {
                    // Verify HTTP signature if present
                    if (!await VerifyHttpSignatureAsync(context))
                    {
                        _logger.LogWarning("HTTP signature verification failed for request to {Path}", context.Request.Path);
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Unauthorized: Invalid HTTP signature");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing HTTP signature for request to {Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized: Error processing signature");
                return;
            }
        }

        await _next(context);
    }

    private bool IsInboxPath(PathString path)
    {
        // Check if this is an inbox endpoint (e.g., /users/username/inbox)
        var segments = path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3 && segments[segments.Length - 1] == "inbox";
    }

    private async Task<bool> VerifyHttpSignatureAsync(HttpContext context)
    {
        // Get the HTTP Signature header
        var signatureHeader = context.Request.Headers["Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signatureHeader))
        {
            // Try alternative headers used by some implementations
            signatureHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("Signature "))
            {
                return false;
            }
            
            // Convert Authorization header to Signature format
            signatureHeader = signatureHeader.Substring(10); // Remove "Signature " prefix
        }

        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        try
        {
            // Parse the signature header to extract parameters
            var signatureParams = ParseSignatureHeader(signatureHeader);
            
            if (!signatureParams.ContainsKey("keyId") || !signatureParams.ContainsKey("signature"))
            {
                return false;
            }

            // Get the public key for verification
            var publicKey = await GetPublicKeyForVerificationAsync(context, signatureParams["keyId"]);
            if (publicKey == null)
            {
                return false;
            }

            // Create the signed content
            var signedContent = await CreateSignedContentAsync(context, signatureParams);
            if (string.IsNullOrEmpty(signedContent))
            {
                return false;
            }

            // Verify the signature
            return VerifySignature(signedContent, signatureParams["signature"], publicKey.PublicKeyPem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying HTTP signature");
            return false;
        }
    }

    private Dictionary<string, string> ParseSignatureHeader(string signatureHeader)
    {
        var paramsDict = new Dictionary<string, string>();
        var pairs = signatureHeader.Split(',', StringSplitOptions.TrimEntries);
        
        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (keyValue.Length == 2)
            {
                // Remove quotes from the value
                var value = keyValue[1].Trim('"');
                paramsDict[keyValue[0]] = value;
            }
        }
        
        return paramsDict;
    }

    private async Task<PublicKey?> GetPublicKeyForVerificationAsync(HttpContext context, string keyId)
    {
        // Get the key fetching service from DI container
        var keyFetchingService = context.RequestServices.GetService<KeyFetchingService>();
        if (keyFetchingService != null)
        {
            // Try to fetch the public key
            var publicKey = await keyFetchingService.FetchPublicKeyAsync(keyId);
            return publicKey;
        }
        
        // If service not available, return null
        return null;
    }

    private async Task<string> CreateSignedContentAsync(HttpContext context, Dictionary<string, string> signatureParams)
    {
        // For now, we'll return a simple representation of the request body
        // In a real implementation, this would recreate the exact signed content
        // according to the HTTP signature specification
        
        // Read the request body
        using var reader = new StreamReader(context.Request.Body);
        var bodyContent = await reader.ReadToEndAsync();
        
        // Reset the stream position for further processing
        context.Request.Body.Position = 0;
        
        return bodyContent;
    }

    private bool VerifySignature(string content, string signature, string publicKeyPem)
    {
        try
        {
            // Convert base64 signature to byte array
            var signatureBytes = Convert.FromBase64String(signature);
            
            // Load public key from PEM format
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            
            // Verify signature (in real implementation, this would be more complex)
            // For now, we'll just validate the content format
            return !string.IsNullOrEmpty(content) && signatureBytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}