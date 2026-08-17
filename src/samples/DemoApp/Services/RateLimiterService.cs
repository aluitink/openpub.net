using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Caching.Memory;

namespace DemoApp.Services;

public class RateLimitOptions
{
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
    public int MaxRequests { get; set; } = 100;
}

public class EndpointRateLimit
{
    public string Endpoint { get; set; } = string.Empty;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
    public int MaxRequests { get; set; } = 100;
}

public class RateLimitState
{
    public DateTime WindowStart { get; set; } = DateTime.MinValue;
    public int RequestCount { get; set; }
}

public class RateLimiterService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, RateLimitState> _clientStates;
    private readonly object _lock = new();
    private readonly List<EndpointRateLimit> _endpointLimits;

    public RateLimiterService(IMemoryCache cache)
    {
        _cache = cache;
        _clientStates = new ConcurrentDictionary<string, RateLimitState>();
        _endpointLimits = new List<EndpointRateLimit>();
    }

    public void AddEndpointLimit(string endpoint, TimeSpan window, int maxRequests)
    {
        _endpointLimits.Add(new EndpointRateLimit
        {
            Endpoint = endpoint,
            Window = window,
            MaxRequests = maxRequests
        });
    }

    public void SetDefaultLimit(TimeSpan window, int maxRequests)
    {
        _cache.Set("default_rate_limit", new RateLimitOptions { Window = window, MaxRequests = maxRequests }, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = window * 2 });
    }

    public (bool allowed, int remaining, DateTime ResetTimeVal) TryAcquire(string clientKey, string endpoint = "")
    {
        var defaultOptions = _cache.GetOrCreate<RateLimitOptions>("default_rate_limit", _ => new RateLimitOptions()) ?? new RateLimitOptions();

        var now = DateTime.UtcNow;
        var state = _clientStates.GetOrAdd(clientKey, _ => new RateLimitState());

        lock (_lock)
        {
            if (now - state.WindowStart > defaultOptions.Window)
            {
                state.RequestCount = 0;
                state.WindowStart = now;
            }

            if (state.RequestCount >= defaultOptions.MaxRequests)
            {
                return (false, 0, state.WindowStart + defaultOptions.Window);
            }

            state.RequestCount++;
            var remaining = defaultOptions.MaxRequests - state.RequestCount;

            return (true, remaining, state.WindowStart + defaultOptions.Window);
        }
    }

    public Dictionary<string, object> GetRateLimitInfo(string clientKey)
    {
        if (!_clientStates.TryGetValue(clientKey, out var state))
        {
            return new Dictionary<string, object>
            {
                { "clientKey", clientKey },
                { "requestCount", 0 },
                { "maxRequests", 0 },
                { "windowStart", DateTime.MinValue },
                { "resetTime", DateTime.MinValue }
            };
        }

        return new Dictionary<string, object>
        {
            { "clientKey", clientKey },
            { "requestCount", state.RequestCount },
            { "maxRequests", 100 },
            { "windowStart", state.WindowStart },
            { "resetTime", state.WindowStart + TimeSpan.FromMinutes(1) }
        };
    }

    public List<EndpointRateLimit> GetEndpointLimits()
    {
        return _endpointLimits;
    }
}
