using ActivityPub.Core.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ActivityPub.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="RateLimitingMiddleware"/> — the ActivityPub
/// rate limiter, which previously had no direct unit test. Drives the
/// middleware with a <see cref="DefaultHttpContext"/> and asserts on the
/// response status and whether the downstream delegate ran.
/// </summary>
public class RateLimitingMiddlewareTests
{
    private static (RateLimitingMiddleware middleware, DefaultHttpContext context, Func<Task> next) Build(
        RateLimitOptions options,
        string path = "/inbox",
        string? authHeader = null,
        string remoteIp = "1.2.3.4")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);
        if (authHeader != null)
            context.Request.Headers["Authorization"] = authHeader;

        bool nextRan = false;
        RequestDelegate next = _ =>
        {
            nextRan = true;
            return Task.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(
            next,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RateLimitingMiddleware>.Instance,
            options);

        return (middleware, context, () => Task.CompletedTask);
    }

    [Fact]
    public async Task UnderLimit_PassesThrough()
    {
        var options = new RateLimitOptions { MaxRequests = 5, Window = TimeSpan.FromMinutes(1) };
        var (mw, ctx, _) = Build(options);

        await mw.InvokeAsync(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ExceedsLimit_Returns429()
    {
        var options = new RateLimitOptions { MaxRequests = 3, Window = TimeSpan.FromMinutes(1) };
        var (mw, ctx, _) = Build(options);

        // 3 allowed...
        for (var i = 0; i < 3; i++)
            await mw.InvokeAsync(ctx);

        // ...4th is rejected.
        await mw.InvokeAsync(ctx);

        Assert.Equal(429, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task DifferentIps_TrackedIndependently()
    {
        var options = new RateLimitOptions { MaxRequests = 2, Window = TimeSpan.FromMinutes(1) };
        var (mw, ctxA, _) = Build(options, remoteIp: "10.0.0.1");
        var (_, ctxB, _) = Build(options, remoteIp: "10.0.0.2");

        // Exhaust client A.
        await mw.InvokeAsync(ctxA);
        await mw.InvokeAsync(ctxA);
        await mw.InvokeAsync(ctxA); // 429 for A
        Assert.Equal(429, ctxA.Response.StatusCode);

        // Client B is still under the limit.
        await mw.InvokeAsync(ctxB);
        Assert.Equal(200, ctxB.Response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationKeyId_UsedAsClientKey_NotIp()
    {
        var options = new RateLimitOptions { MaxRequests = 2, Window = TimeSpan.FromMinutes(1) };
        var (mw, ctxA, _) = Build(options, authHeader: "Signature keyId=\"key-1\",signature=\"x\"", remoteIp: "10.0.0.1");
        var (_, ctxB, _) = Build(options, authHeader: "Signature keyId=\"key-2\",signature=\"x\"", remoteIp: "10.0.0.2");

        await mw.InvokeAsync(ctxA);
        await mw.InvokeAsync(ctxA);
        await mw.InvokeAsync(ctxA); // 429 for key-1
        Assert.Equal(429, ctxA.Response.StatusCode);

        // key-2 is a distinct client (even though it came from a different IP).
        await mw.InvokeAsync(ctxB);
        Assert.Equal(200, ctxB.Response.StatusCode);
    }

    [Fact]
    public async Task SameKeyId_DifferentIps_ShareLimit()
    {
        var options = new RateLimitOptions { MaxRequests = 2, Window = TimeSpan.FromMinutes(1) };
        var (mw, ctxA, _) = Build(options, authHeader: "Signature keyId=\"shared\",signature=\"x\"", remoteIp: "10.0.0.1");
        var (_, ctxB, _) = Build(options, authHeader: "Signature keyId=\"shared\",signature=\"x\"", remoteIp: "10.0.0.2");

        await mw.InvokeAsync(ctxA);
        await mw.InvokeAsync(ctxB);
        await mw.InvokeAsync(ctxA); // shared limit exhausted -> 429

        Assert.Equal(429, ctxA.Response.StatusCode);
    }

    [Fact]
    public async Task PathFilter_ExcludedPath_NotLimited()
    {
        // Limit is tiny, but the requested path is not in the limited set.
        var options = new RateLimitOptions { MaxRequests = 1, Window = TimeSpan.FromMinutes(1), Paths = new[] { "/inbox" } };
        var (mw, ctx, _) = Build(options, path: "/other");

        // Even many requests on an unlisted path never hit the limit.
        for (var i = 0; i < 10; i++)
            await mw.InvokeAsync(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task PathFilter_IncludedPath_IsLimited()
    {
        var options = new RateLimitOptions { MaxRequests = 1, Window = TimeSpan.FromMinutes(1), Paths = new[] { "/inbox" } };
        var (mw, ctx, _) = Build(options, path: "/inbox");

        await mw.InvokeAsync(ctx); // allowed
        await mw.InvokeAsync(ctx); // 429

        Assert.Equal(429, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task PathFilter_MatchesSubPaths()
    {
        // "/inbox" should also rate-limit "/inbox/sub".
        var options = new RateLimitOptions { MaxRequests = 1, Window = TimeSpan.FromMinutes(1), Paths = new[] { "/inbox" } };
        var (mw, ctx, _) = Build(options, path: "/inbox/sub");

        await mw.InvokeAsync(ctx);
        await mw.InvokeAsync(ctx);

        Assert.Equal(429, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task CountAccumulatesWithinOpenWindow_ThenResets()
    {
        // Within a single open (not-yet-expired) window the count accumulates
        // until MaxRequests, after which the client is throttled (429). A
        // different client (fresh window) is unaffected.
        var options = new RateLimitOptions { MaxRequests = 2, Window = TimeSpan.FromMinutes(1) };
        var (mw, ctxA, _) = Build(options, remoteIp: "10.0.0.1");
        var (_, ctxB, _) = Build(options, remoteIp: "10.0.0.2");

        await mw.InvokeAsync(ctxA); // count 1
        await mw.InvokeAsync(ctxA); // count 2
        await mw.InvokeAsync(ctxA); // count 3 -> throttled
        Assert.Equal(429, ctxA.Response.StatusCode);

        // Client B has its own window and is not throttled by A's usage.
        await mw.InvokeAsync(ctxB);
        Assert.Equal(200, ctxB.Response.StatusCode);
    }

    [Fact]
    public async Task TrackedClientCount_ReflectsDistinctClients()
    {
        var options = new RateLimitOptions { MaxRequests = 100, Window = TimeSpan.FromMinutes(1) };
        var (mw, ctxA, _) = Build(options, remoteIp: "10.0.0.1");
        var (_, ctxB, _) = Build(options, remoteIp: "10.0.0.2");
        var (_, ctxC, _) = Build(options, remoteIp: "10.0.0.3");

        Assert.Equal(0, mw.TrackedClientCount);

        await mw.InvokeAsync(ctxA);
        await mw.InvokeAsync(ctxB);
        await mw.InvokeAsync(ctxC);

        Assert.Equal(3, mw.TrackedClientCount);
    }
}
