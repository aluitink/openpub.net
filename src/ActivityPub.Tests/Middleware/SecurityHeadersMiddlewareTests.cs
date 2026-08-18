using ActivityPub.Core.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ActivityPub.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="SecurityHeadersMiddleware"/> — the security
/// headers middleware, which previously had no direct unit test. Drives the
/// middleware with a <see cref="DefaultHttpContext"/> and asserts on the
/// response headers it sets (and that the downstream delegate always runs).
/// </summary>
public class SecurityHeadersMiddlewareTests
{
    private static (SecurityHeadersMiddleware middleware, DefaultHttpContext context, Func<int> nextCallCount) Build(
        bool isHttps)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = isHttps ? "https" : "http";

        int calls = 0;
        RequestDelegate next = _ =>
        {
            calls++;
            return Task.CompletedTask;
        };

        var middleware = new SecurityHeadersMiddleware(
            next,
            NullLogger<SecurityHeadersMiddleware>.Instance);

        return (middleware, context, () => calls);
    }

    [Fact]
    public async Task SetsStaticSecurityHeaders()
    {
        var (mw, ctx, nextCallCount) = Build(isHttps: false);

        await mw.InvokeAsync(ctx);

        Assert.Equal("nosniff", ctx.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", ctx.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("1; mode=block", ctx.Response.Headers["X-XSS-Protection"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", ctx.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("geolocation=(), microphone=(), camera=()", ctx.Response.Headers["Permissions-Policy"].ToString());
        Assert.Equal("no-store, no-cache, must-revalidate, max-age=0", ctx.Response.Headers["Cache-Control"].ToString());
        Assert.Equal("no-cache", ctx.Response.Headers["Pragma"].ToString());

        var csp = ctx.Response.Headers["Content-Security-Policy"].ToString();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    [Fact]
    public async Task HttpRequest_DoesNotSetHsts()
    {
        var (mw, ctx, nextCallCount) = Build(isHttps: false);

        await mw.InvokeAsync(ctx);

        Assert.False(ctx.Response.Headers.ContainsKey("Strict-Transport-Security"),
            "Strict-Transport-Security must NOT be set on a plain-HTTP response (browsers ignore it, and it signals an invalid configuration)");
        Assert.True(nextCallCount() == 1, "the downstream delegate should always run");
    }

    [Fact]
    public async Task HttpsRequest_SetsHsts()
    {
        var (mw, ctx, nextCallCount) = Build(isHttps: true);

        await mw.InvokeAsync(ctx);

        Assert.Equal("max-age=31536000; includeSubDomains",
            ctx.Response.Headers["Strict-Transport-Security"].ToString());
    }

    [Fact]
    public async Task AlwaysInvokesNext()
    {
        var (mw, ctx, nextCallCount) = Build(isHttps: true);

        await mw.InvokeAsync(ctx);

        Assert.True(nextCallCount() == 1, "the downstream delegate should run exactly once");
    }
}
