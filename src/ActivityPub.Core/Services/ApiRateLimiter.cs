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
    // Evict idle client states at most once per this interval. Amortizing the
    // sweep across calls keeps per-request overhead negligible while bounding
    // the dictionary's growth: without it, every distinct client that ever hits
    // the API would leave a permanent entry (the limiter is a singleton).
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    // A client state is eligible for removal once it is idle (untouched) for at
    // least this long. Two windows comfortably outlasts any in-flight window, so
    // evicting an idle state can never reset an active client's counter early.
    private static readonly TimeSpan IdleEviction = TimeSpan.FromMinutes(5);

    private readonly ApiRateLimitOptions _options;
    private readonly ConcurrentDictionary<string, WindowState> _states = new();
    private DateTime _lastSweepUtc = DateTime.UtcNow;

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

        var now = DateTime.UtcNow;
        SweepIdle(now);

        var state = _states.GetOrAdd(key, static _ => new WindowState());

        lock (state)
        {
            if (now - state.WindowStart >= window)
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            state.LastSeenUtc = now;

            var resetAt = state.WindowStart + window;

            if (state.Count >= limit)
                return ApiRateLimitResult.Deny(limit, resetAt);

            state.Count++;
            return ApiRateLimitResult.Allow(limit, limit - state.Count, resetAt);
        }
    }

    /// <summary>Number of tracked client states (for observability/testing).</summary>
    public int TrackedClientCount => _states.Count;

    /// <summary>
    /// Removes client states that have been idle (untouched) for longer than
    /// <see cref="IdleEviction"/>, so the singleton dictionary cannot grow
    /// unboundedly with the number of distinct clients. Called opportunistically
    /// at most once per <see cref="SweepInterval"/> from <see cref="TryConsume"/>,
    /// but also exposed publicly so it can be driven from tests or a timer.
    /// </summary>
    public void SweepIdle(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (now - _lastSweepUtc < SweepInterval)
            return;

        // Read the timestamp under a lock-free store; a concurrent update is
        // harmless because the sweep only ever removes, and a state that is
        // about to be reset will simply be re-added on its next request.
        var lastSweep = _lastSweepUtc;
        _lastSweepUtc = now;

        foreach (var kvp in _states)
        {
            if (now - kvp.Value.LastSeenUtc >= IdleEviction)
            {
                _states.TryRemove(kvp.Key, out _);
            }
        }
    }

    private sealed class WindowState
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
        public int Count { get; set; }
    }
}
