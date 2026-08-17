using System.Security.Claims;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActivityPub.Core.Middleware;

/// <summary>
/// Applies <see cref="ApiRateLimiter"/> to the local Mastodon-shaped REST API
/// (<c>/api/v1/*</c>). The rate-limit key is the authenticated identity:
/// the OAuth <c>client_id</c> for Bearer-token requests, the username for
/// cookie-session requests, and the client IP for anything else. Mastodon-style
/// <c>RateLimit-</c> headers are set on every API response; exceeding the limit
/// yields <c>429 Too Many Requests</c>.
/// </summary>
public class ApiRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiRateLimitingMiddleware> _logger;
    private readonly ApiRateLimiter _limiter;
    private readonly ApiRateLimitOptions _options;

    public ApiRateLimitingMiddleware(
        RequestDelegate next,
        ILogger<ApiRateLimitingMiddleware> logger,
        ApiRateLimiter limiter,
        IOptions<ApiRateLimitOptions> options)
    {
        _next = next;
        _logger = logger;
        _limiter = limiter;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1"))
        {
            await _next(context);
            return;
        }

        var clientKey = ResolveClientKey(context);
        var applicationClientId = GetApplicationClientId(context);
        var result = _limiter.TryConsume(clientKey, applicationClientId);

        var resetHeader = result.ResetAtUtc > DateTime.MinValue
            ? new DateTimeOffset(result.ResetAtUtc).ToUnixTimeSeconds().ToString()
            : "0";

        context.Response.Headers["RateLimit-Limit"] = result.Limit > 0 ? result.Limit.ToString() : "0";
        context.Response.Headers["RateLimit-Remaining"] = result.Remaining.ToString();
        context.Response.Headers["RateLimit-Reset"] = resetHeader;

        if (!result.Allowed)
        {
            _logger.LogInformation(
                "API rate limit exceeded for client {ClientKey} (app {App})", clientKey, applicationClientId ?? "-");
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Too Many Requests",
                error_description = "Rate limit exceeded. Please slow down and try again."
            });
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// The identity used as the primary rate-limit bucket key.
    /// </summary>
    private static string ResolveClientKey(HttpContext context)
    {
        var principal = context.User;
        if (principal?.Identity?.IsAuthenticated == true)
        {
            // Bearer tokens carry the application's client_id as the
            // "api.client_id" claim; cookie sessions carry the username.
            // Prefer client_id so a given app's usage is bucketed together,
            // then fall back to the username.
            var clientId = principal.FindFirst("api.client_id")?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrEmpty(clientId))
                return clientId;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Returns the OAuth client_id when the request is authenticated via a
    /// Bearer token (so per-application limits can be applied), else null.
    /// The "api.client_id" claim is only added by the Bearer-token handler, so
    /// its presence reliably identifies an app-authenticated request.
    /// </summary>
    private static string? GetApplicationClientId(HttpContext context)
    {
        var principal = context.User;
        if (principal?.Identity?.IsAuthenticated == true)
        {
            var clientId = principal.FindFirst("api.client_id")?.Value;
            if (!string.IsNullOrEmpty(clientId))
                return clientId;
        }

        return null;
    }
}

/// <summary>
/// Extension method to register the API rate-limiting middleware.
/// </summary>
public static class ApiRateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Adds API (Mastodon REST) rate limiting to the pipeline. Should be placed
    /// after <c>UseAuthentication()</c> so the authenticated identity is
    /// available for bucketing.
    /// </summary>
    public static IApplicationBuilder UseApiRateLimiting(this IApplicationBuilder app)
        => app.UseMiddleware<ApiRateLimitingMiddleware>();
}
