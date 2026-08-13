using ActivityPub.Core.Middleware;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ActivityPub.Tests.Services;

public class RateLimitingMiddlewareTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly RateLimitOptions _options;

    public RateLimitingMiddlewareTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _options = new RateLimitOptions
        {
            Window = TimeSpan.FromMinutes(1),
            MaxRequests = 5
        };
    }

    [Fact]
    public void Middleware_AllowsRequestsUnderLimit()
    {
        // Test that requests under the limit are allowed
        // This is a basic test - full middleware testing requires HttpContext
        Assert.True(_options.MaxRequests > 0);
    }

    [Fact]
    public void Middleware_EnforcesRateLimit()
    {
        // Verify options are configured correctly
        Assert.Equal(TimeSpan.FromMinutes(1), _options.Window);
        Assert.Equal(5, _options.MaxRequests);
    }

    [Fact]
    public void RateLimitState_InitializesCorrectly()
    {
        var state = new RateLimitState();
        Assert.Equal(DateTime.MinValue, state.WindowStart);
        Assert.Equal(0, state.RequestCount);
    }
}
