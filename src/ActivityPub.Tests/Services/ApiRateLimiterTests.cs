using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Unit tests for the per-client API rate limiter (Mastodon REST API).
/// Drives the limiter directly with small limits so behaviour is
/// deterministic without issuing hundreds of HTTP requests.
/// </summary>
public class ApiRateLimiterTests
{
    private static ApiRateLimiter CreateLimiter(
        int maxRequests = 5,
        TimeSpan? window = null,
        bool enabled = true)
    {
        var options = new ApiRateLimitOptions
        {
            Enabled = enabled,
            MaxRequests = maxRequests,
            Window = window ?? TimeSpan.FromMinutes(1),
        };
        return new ApiRateLimiter(Options.Create(options));
    }

    [Fact]
    public void AllowsRequests_UntilLimitThenDenies()
    {
        var limiter = CreateLimiter(maxRequests: 3);

        for (int i = 0; i < 3; i++)
        {
            var result = limiter.TryConsume("client-a", null);
            Assert.True(result.Allowed, $"Request {i + 1} of 3 should be allowed");
        }

        Assert.Equal(3, limiter.TryConsume("client-a", null).Limit);
        var denied = limiter.TryConsume("client-a", null);
        Assert.False(denied.Allowed, "4th request should be denied");
        Assert.Equal(0, denied.Remaining);
    }

    [Fact]
    public void RemainingCountsDown()
    {
        var limiter = CreateLimiter(maxRequests: 5);

        Assert.Equal(4, limiter.TryConsume("c", null).Remaining);
        Assert.Equal(3, limiter.TryConsume("c", null).Remaining);
        Assert.Equal(2, limiter.TryConsume("c", null).Remaining);
        Assert.Equal(1, limiter.TryConsume("c", null).Remaining);
        Assert.Equal(0, limiter.TryConsume("c", null).Remaining);
        Assert.Equal(0, limiter.TryConsume("c", null).Remaining); // denied, stays 0
    }

    [Fact]
    public void DifferentClientsAreBucketsIndependently()
    {
        var limiter = CreateLimiter(maxRequests: 2);

        // Exhaust client A.
        limiter.TryConsume("client-a", null);
        limiter.TryConsume("client-a", null);
        Assert.False(limiter.TryConsume("client-a", null).Allowed);

        // Client B is unaffected.
        Assert.True(limiter.TryConsume("client-b", null).Allowed);
        Assert.True(limiter.TryConsume("client-b", null).Allowed);
        Assert.False(limiter.TryConsume("client-b", null).Allowed);
    }

    [Fact]
    public void DisabledAllowsEverything()
    {
        var limiter = CreateLimiter(maxRequests: 1, enabled: false);

        for (int i = 0; i < 10; i++)
        {
            var result = limiter.TryConsume("client-a", null);
            Assert.True(result.Allowed, "Disabled limiter must allow all requests");
        }
    }

    [Fact]
    public void PerApplicationOverrideUsesCustomLimit()
    {
        var options = new ApiRateLimitOptions
        {
            MaxRequests = 100,
            Window = TimeSpan.FromMinutes(1),
        };
        options.PerApplication["app-x"] = new ApiRateLimitPolicy { MaxRequests = 2 };
        var limiter = new ApiRateLimiter(Options.Create(options));

        // The app with the override gets only 2.
        Assert.True(limiter.TryConsume("key", "app-x").Allowed);
        Assert.True(limiter.TryConsume("key", "app-x").Allowed);
        Assert.False(limiter.TryConsume("key", "app-x").Allowed);

        // A different app (no override) falls back to the global 100.
        var r = limiter.TryConsume("key", "app-y");
        Assert.True(r.Allowed);
        Assert.Equal(100, r.Limit);
        Assert.Equal(99, r.Remaining);
    }

    [Fact]
    public void ResetAtIsInFutureWithinWindow()
    {
        var limiter = CreateLimiter(maxRequests: 5, window: TimeSpan.FromMinutes(1));
        var result = limiter.TryConsume("c", null);

        var now = DateTime.UtcNow;
        Assert.True(result.ResetAtUtc > now.AddSeconds(-2), "Reset should be in the future");
        Assert.True(result.ResetAtUtc <= now.AddMinutes(1).AddSeconds(2), "Reset should be within the window");
    }
}
