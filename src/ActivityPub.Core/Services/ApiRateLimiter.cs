using System.Collections.Concurrent;
using ActivityPub.Core.Options;
using Microsoft.Extensions.Options;

namespace ActivityPub.Core.Services;

/// <summary>
/// Outcome of an API rate-limit check for a single request.
/// </summary>
public readonly record struct ApiRateLimitResult(
    bool Allowed,
    int Limit,
    int Remaining,
    DateTime ResetAtUtc)
{
    public static ApiRateLimitResult Allow(int limit, int remaining, DateTime resetAtUtc)
        => new(true, limit, remaining, resetAtUtc);

    public static ApiRateLimitResult Deny(int limit, DateTime resetAtUtc)
        => new(false, limit, 0, resetAtUtc);
}

/// <summary>
/// In-memory, per-client fixed-window API rate limiter. Tracks request counts
/// keyed by an opaque client identifier (client_id for Bearer tokens, username
/// for cookie sessions, otherwise the client IP). Limits are configurable
/// globally and per application via <see cref="ApiRateLimitOptions"/>.
/// </summary>
public class ApiRateLimiter
{
    private readonly ApiRateLimitOptions _options;
    private readonly ConcurrentDictionary<string, WindowState> _states = new();

    public ApiRateLimiter(IOptions<ApiRateLimitOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Records a request from <paramref name="clientId"/> and reports whether it
    /// is allowed under the client's effective policy.
    /// </summary>
    public ApiRateLimitResult TryConsume(string clientId, string? applicationClientId)
    {
        if (!_options.Enabled)
            return ApiRateLimitResult.Allow(0, 0, DateTime.MinValue);

        var (limit, window) = _options.Resolve(applicationClientId);
        var key = clientId + (applicationClientId is null ? "" : "|" + applicationClientId);

        var state = _states.GetOrAdd(key, static _ => new WindowState());
        var now = DateTime.UtcNow;

        lock (state)
        {
            if (now - state.WindowStart >= window)
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            var resetAt = state.WindowStart + window;

            if (state.Count >= limit)
                return ApiRateLimitResult.Deny(limit, resetAt);

            state.Count++;
            return ApiRateLimitResult.Allow(limit, limit - state.Count, resetAt);
        }
    }

    private sealed class WindowState
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public int Count { get; set; }
    }
}
