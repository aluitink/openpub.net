using System.Collections.Concurrent;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;

namespace ActivityPub.Core.Middleware;

/// <summary>
/// Rate limiting middleware for ActivityPub endpoints
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitOptions _options;

    private readonly ConcurrentDictionary<string, RateLimitState> _clientStates;
    private readonly object _lock = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
        _clientStates = new ConcurrentDictionary<string, RateLimitState>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_options.Paths.Length > 0 && !_options.Paths.Any(p =>
            context.Request.Path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var clientKey = GetClientKey(context, clientIp);

        if (!TryProcessRequest(clientKey, out var state))
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Rate limit exceeded");
            _logger.LogWarning("Rate limit exceeded for client {ClientKey}", clientKey);
            return;
        }

        await _next(context);
    }

    private string GetClientKey(HttpContext context, string clientIp)
    {
        // Try to get actor ID from authorization header for authenticated requests
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader) && !string.IsNullOrEmpty(authHeader))
        {
            // Extract actor URL from keyId in signature
            var authValue = authHeader.ToString();
            if (authValue.Contains("keyId="))
            {
                var start = authValue.IndexOf("keyId=\"") + 7;
                var end = authValue.IndexOf("\"", start);
                if (start > 6 && end > start)
                {
                    var keyId = authValue.Substring(start, end - start);
                    return keyId;
                }
            }
        }

        return clientIp;
    }

    private bool TryProcessRequest(string clientKey, out RateLimitState state)
    {
        state = _clientStates.GetOrAdd(clientKey, _ => new RateLimitState());

        lock (_lock)
        {
            var now = DateTime.UtcNow;

            // Reset window if expired
            if (now - state.WindowStart > _options.Window)
            {
                state.RequestCount = 0;
                state.WindowStart = now;
            }

            // Check if rate limit exceeded
            if (state.RequestCount >= _options.MaxRequests)
            {
                return false;
            }

            state.RequestCount++;
            return true;
        }
    }
}

/// <summary>
/// Rate limit options
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Time window for rate limiting (default: 1 minute)
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum requests per window (default: 100)
    /// </summary>
    public int MaxRequests { get; set; } = 100;

    /// <summary>
    /// Paths to apply rate limiting to. If empty, applies to all paths.
    /// </summary>
    public string[] Paths { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Rate limit state per client
/// </summary>
public class RateLimitState
{
    public DateTime WindowStart { get; set; } = DateTime.MinValue;
    public int RequestCount { get; set; }
}

/// <summary>
/// Extension methods for rate limiting middleware
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Add rate limiting middleware to pipeline
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }

    /// <summary>
    /// Add rate limiting middleware with options
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, Action<RateLimitOptions> configure)
    {
        var options = new RateLimitOptions();
        configure(options);
        return app.UseMiddleware<RateLimitingMiddleware>(options);
    }
}
