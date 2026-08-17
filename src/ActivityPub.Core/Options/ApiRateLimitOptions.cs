using System.Collections.Concurrent;

namespace ActivityPub.Core.Options;

/// <summary>
/// Configuration for API (Mastodon-shaped <c>/api/v1</c>) rate limiting.
/// Binds from the <c>"ApiRateLimit"</c> configuration section in
/// appsettings.json so limits can be tuned without code changes.
/// </summary>
public class ApiRateLimitOptions
{
    /// <summary>Master switch. When false, no API rate limiting is applied.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Time window for the request counter (default: 1 minute).</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Default maximum number of requests per client per window (default: 300,
    /// matching Mastodon's default of 300 requests/minute).
    /// </summary>
    public int MaxRequests { get; set; } = 300;

    /// <summary>
    /// Optional per-application overrides keyed by <c>client_id</c>. An
    /// application registered via <c>POST /api/v1/apps</c> that has an entry
    /// here uses those limits instead of the defaults.
    /// </summary>
    public ConcurrentDictionary<string, ApiRateLimitPolicy> PerApplication { get; set; } = new();

    /// <summary>
    /// Returns the effective policy for a client (falling back to the global
    /// defaults when no override is configured for that client_id).
    /// </summary>
    public (int maxRequests, TimeSpan window) Resolve(string? clientId)
    {
        if (clientId != null && PerApplication.TryGetValue(clientId, out var policy) && policy.MaxRequests > 0)
            return (policy.MaxRequests, policy.Window > TimeSpan.Zero ? policy.Window : Window);

        return (MaxRequests, Window);
    }
}

/// <summary>
/// A per-application rate limit policy (client_id-scoped override).
/// </summary>
public class ApiRateLimitPolicy
{
    /// <summary>Maximum requests per window for this application.</summary>
    public int MaxRequests { get; set; }

    /// <summary>
    /// Optional window override. When not set (zero), the global
    /// <see cref="ApiRateLimitOptions.Window"/> is used.
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.Zero;
}
