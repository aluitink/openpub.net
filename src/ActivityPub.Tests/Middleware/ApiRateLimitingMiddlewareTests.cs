using System.Security.Claims;
using ActivityPub.Core.Middleware;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="ApiRateLimitingMiddleware"/> — the rate limiter
/// for the local Mastodon-shaped REST API (<c>/api/v1/*</c>). Drives the
/// middleware with a single real <see cref="ApiRateLimiter"/> (shared across
/// contexts, over real <see cref="ApiRateLimitOptions"/>) and a
/// <see cref="DefaultHttpContext"/>, asserting on the Mastodon-style
/// <c>RateLimit-*</c> headers, the 429 body, and the identity-based bucketing
/// (Bearer client_id vs username vs IP).
/// </summary>
public class ApiRateLimitingMiddlewareTests
{
    /// <summary>
    /// Builds one or more contexts that all share the same <see cref="ApiRateLimiter"/>
    /// (so cross-context bucketing is observable) and the same options.
    /// </summary>
    private static ApiRateLimiter BuildLimiter(ApiRateLimitOptions options) =>
        new(Options.Create(options));

    private static (DefaultHttpContext ctx, Func<int> nextCallCount, ApiRateLimitingMiddleware mw) BuildContext(
        ApiRateLimiter limiter,
        ApiRateLimitOptions options,
        string path = "/api/v1/timelines/home",
        ClaimsPrincipal? user = null,
        string remoteIp = "1.2.3.4")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);
        if (user != null)
            ctx.User = user;
        // DefaultHttpContext exposes a NullStream for the response body, so
        // WriteAsJsonAsync would be discarded. Install a seekable in-memory
        // stream so the 429 JSON body can be read back.
        ctx.Response.Body = new MemoryStream();

        int calls = 0;
        RequestDelegate next = _ =>
        {
            calls++;
            return Task.CompletedTask;
        };

        var mw = new ApiRateLimitingMiddleware(
            next,
            NullLogger<ApiRateLimitingMiddleware>.Instance,
            limiter,
            Options.Create(options));

        return (ctx, () => calls, mw);
    }

    /// <summary>Reads the (in-memory) response body and restores the stream position.</summary>
    private static string Body(DefaultHttpContext ctx)
    {
        var original = ctx.Response.Body;
        original.Position = 0; // CopyTo reads from the current position; reset to the start.
        var ms = new MemoryStream();
        original.CopyTo(ms);
        original.Position = 0;
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static ClaimsPrincipal Bearer(string clientId) =>
        new(new ClaimsIdentity(new[] { new Claim("api.client_id", clientId) }, "Bearer"));

    private static ClaimsPrincipal Cookie(string username) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }, "Cookie"));

    [Fact]
    public async Task NonApiPath_IsNotLimited()
    {
        var options = new ApiRateLimitOptions { MaxRequests = 1 };
        var (ctx, nextCallCount, mw) = BuildContext(BuildLimiter(options), options, path: "/users/alice");

        // Even many requests on a non-/api/v1 path bypass the limiter entirely.
        for (var i = 0; i < 5; i++)
            await mw.InvokeAsync(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.True(nextCallCount() == 5, "the downstream delegate should always run for non-API paths");
        Assert.False(ctx.Response.Headers.ContainsKey("RateLimit-Limit"), "no RateLimit headers on non-API paths");
    }

    [Fact]
    public async Task UnderLimit_SetsHeadersAndPassesThrough()
    {
        var options = new ApiRateLimitOptions { MaxRequests = 10 };
        var (ctx, nextCallCount, mw) = BuildContext(BuildLimiter(options), options);

        await mw.InvokeAsync(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.True(nextCallCount() == 1);
        Assert.Equal("10", ctx.Response.Headers["RateLimit-Limit"].ToString());
        Assert.Equal("9", ctx.Response.Headers["RateLimit-Remaining"].ToString());
        Assert.False(string.IsNullOrEmpty(ctx.Response.Headers["RateLimit-Reset"].ToString()));
    }

    [Fact]
    public async Task ExceedsLimit_Returns429WithJsonBody()
    {
        var options = new ApiRateLimitOptions { MaxRequests = 2 };
        var (ctx, nextCallCount, mw) = BuildContext(BuildLimiter(options), options);

        await mw.InvokeAsync(ctx); // 1
        await mw.InvokeAsync(ctx); // 2
        await mw.InvokeAsync(ctx); // 3 -> denied

        Assert.Equal(429, ctx.Response.StatusCode);
        Assert.True(nextCallCount() == 2, "the delegate should run only for the 2 allowed requests");
        Assert.Equal("0", ctx.Response.Headers["RateLimit-Remaining"].ToString());

        using var doc = JsonDocument.Parse(Body(ctx));
        Assert.Equal("Too Many Requests", doc.RootElement.GetProperty("error").GetString());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("error_description").GetString()));
    }

    [Fact]
    public async Task BearerClientIds_AreBucketedTogether()
    {
        var options = new ApiRateLimitOptions { MaxRequests = 2 };
        var limiter = BuildLimiter(options);
        var (ctxA, nextA, mwA) = BuildContext(limiter, options, user: Bearer("app-1"), remoteIp: "10.0.0.1");
        var (ctxB, nextB, mwB) = BuildContext(limiter, options, user: Bearer("app-1"), remoteIp: "10.0.0.2");

        await mwA.InvokeAsync(ctxA);
        await mwB.InvokeAsync(ctxB); // same app-1 bucket, different IP
        await mwA.InvokeAsync(ctxA); // app-1 exhausted -> 429

        Assert.Equal(429, ctxA.Response.StatusCode);
        Assert.True(nextA() == 1 && nextB() == 1, "each app-1 request counted toward the shared bucket");
    }

    [Fact]
    public async Task DifferentClientIds_AreIndependent()
    {
        var options = new ApiRateLimitOptions { MaxRequests = 2 };
        var limiter = BuildLimiter(options);
        var (ctxA, _, mwA) = BuildContext(limiter, options, user: Bearer("app-1"), remoteIp: "10.0.0.1");
        var (ctxB, _, mwB) = BuildContext(limiter, options, user: Bearer("app-2"), remoteIp: "10.0.0.2");

        await mwA.InvokeAsync(ctxA);
        await mwA.InvokeAsync(ctxA);
        await mwA.InvokeAsync(ctxA); // app-1 exhausted
        Assert.Equal(429, ctxA.Response.StatusCode);

        await mwB.InvokeAsync(ctxB); // app-2 unaffected
        Assert.Equal(200, ctxB.Response.StatusCode);
    }

    [Fact]
    public async Task CookieUsernames_AreBucketedSeparatelyFromApps()
    {
        var options = new ApiRateLimitOptions { MaxRequests = 1 };
        var limiter = BuildLimiter(options);
        var (ctxUser, _, mwUser) = BuildContext(limiter, options, user: Cookie("alice"), remoteIp: "10.0.0.1");
        var (ctxApp, _, mwApp) = BuildContext(limiter, options, user: Bearer("app-1"), remoteIp: "10.0.0.2");

        await mwUser.InvokeAsync(ctxUser);
        await mwUser.InvokeAsync(ctxUser); // alice exhausted
        Assert.Equal(429, ctxUser.Response.StatusCode);

        await mwApp.InvokeAsync(ctxApp); // app-1 has its own bucket
        Assert.Equal(200, ctxApp.Response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_UsesIpAsKey()
    {
        var options = new ApiRateLimitOptions { MaxRequests = 1 };
        var limiter = BuildLimiter(options);
        var (ctxA, _, mwA) = BuildContext(limiter, options, user: null, remoteIp: "10.0.0.1");
        var (ctxB, _, mwB) = BuildContext(limiter, options, user: null, remoteIp: "10.0.0.2");

        await mwA.InvokeAsync(ctxA);
        await mwA.InvokeAsync(ctxA); // IP 10.0.0.1 exhausted
        Assert.Equal(429, ctxA.Response.StatusCode);

        await mwB.InvokeAsync(ctxB); // different IP -> different bucket
        Assert.Equal(200, ctxB.Response.StatusCode);
    }
}
