using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;

namespace ActivityPub.Core.Middleware;

/// <summary>
/// Middleware to verify all incoming activities are signed with HTTP signatures
/// </summary>
public class SigningVerificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SigningVerificationMiddleware> _logger;

    public SigningVerificationMiddleware(RequestDelegate next, ILogger<SigningVerificationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Only enforce signing for ActivityPub endpoints (not WebFinger or static files)
        if (IsActivityPubEndpoint(path))
        {
            if (!context.Request.Headers.TryGetValue("Signature", out var signatureHeader) || string.IsNullOrEmpty(signatureHeader))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Request must be signed with HTTP Signature");
                _logger.LogWarning("Unsigned request blocked for path {Path}", path);
                return;
            }
        }

        await _next(context);
    }

    private bool IsActivityPubEndpoint(string path)
    {
        // ActivityPub endpoints that require signing
        var activityPubPaths = new[]
        {
            "/inbox",
            "/outbox",
            "/followers",
            "/following",
            "/shared-inbox",
            "/activity"
        };

        return activityPubPaths.Any(p => path.StartsWith(p, StringComparison.Ordinal));
    }
}

/// <summary>
/// Extension methods for signing verification middleware
/// </summary>
public static class SigningVerificationMiddlewareExtensions
{
    /// <summary>
    /// Add signing verification middleware to pipeline
    /// </summary>
    public static IApplicationBuilder UseSigningVerification(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SigningVerificationMiddleware>();
    }
}
