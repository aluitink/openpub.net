using ActivityPub.Core.API.Controllers.WellKnown;
using ActivityPub.Core.Caching;
using ActivityPub.Core.Events;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="WebFingerController"/> — the canonical
/// <c>/.well-known/webfinger</c> endpoint, which previously had no direct
/// unit test (only the <c>/v1</c> versioned controller was covered).
/// Drives the controller with a <see cref="DefaultHttpContext"/> and a real
/// <see cref="WebFingerCacheService"/> (over an in-memory cache) and asserts
/// on the returned <see cref="IActionResult"/> / JSON.
/// </summary>
public class WebFingerControllerTests
{
    private static (WebFingerController controller, WebFingerCacheService cache) Build(string host = "example.com", string scheme = "https")
    {
        var cache = new WebFingerCacheService(new MemoryCache(new MemoryCacheOptions()));
        // ActivityPubService is a concrete, non-sealed class whose members are
        // not virtual, so Moq cannot proxy it. The controller never calls it
        // (the field is only stored), so a real instance with real/mocked
        // dependencies is sufficient.
        var federationCache = new MemoryFederationCache(new MemoryCache(new MemoryCacheOptions()));
        var activityPubService = new ActivityPubService(
            new InMemoryActivityPubRepository(),
            new ActivityPub.Core.Services.ActivityPubEventDispatcher(),
            new ActivityPub.Core.Events.IActivityPubInterceptor[0],
            NullLogger<ActivityPubService>.Instance,
            federationCache,
            new CacheInvalidationService(federationCache, NullLogger<CacheInvalidationService>.Instance));

        var controller = new WebFingerController(
            cache,
            Options.Create(new ActivityPubOptions { Domain = "fallback.example.com", UserPath = "/users" }),
            activityPubService);

        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        return (controller, cache);
    }

    [Fact]
    public async Task MissingResource_Returns400()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: null, rel: null) as ContentResult;

        Assert.NotNull(result);
        Assert.Equal(400, result!.StatusCode);
        Assert.Contains("resource parameter is required", result.Content);
        Assert.Equal("application/json", result.ContentType);
    }

    [Fact]
    public async Task EmptyResource_Returns400()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: "", rel: null) as ContentResult;

        Assert.NotNull(result);
        Assert.Equal(400, result!.StatusCode);
    }

    [Fact]
    public async Task MalformedAcct_WithoutAt_Returns404()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: "acct:nouser", rel: null) as ContentResult;

        Assert.NotNull(result);
        Assert.Equal(404, result!.StatusCode);
        Assert.Contains("Invalid resource format", result.Content);
    }

    [Fact]
    public async Task Acct_Resource_ReturnsJrdWithSelfLink()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: "acct:alice@example.com", rel: null) as ContentResult;

        Assert.NotNull(result);
        // The success path returns a ContentResult carrying the JRD; the MVC
        // pipeline assigns the 200 status, so we assert on the content type
        // and body rather than the (unset) status code.
        Assert.Equal("application/jrd+json", result!.ContentType);

        using var doc = JsonDocument.Parse(result.Content);
        Assert.Equal("acct:alice@example.com", doc.RootElement.GetProperty("subject").GetString());

        var links = doc.RootElement.GetProperty("links");
        Assert.Equal(JsonValueKind.Array, links.ValueKind);
        Assert.Equal(5, links.GetArrayLength());

        // The self link points at the user's ActivityPub profile with the
        // request scheme + host (https://example.com/users/alice).
        var self = links.EnumerateArray().First(l => l.GetProperty("rel").GetString() == "self");
        Assert.Equal("application/activity+json", self.GetProperty("type").GetString());
        Assert.Equal("https://example.com/users/alice", self.GetProperty("href").GetString());
    }

    [Fact]
    public async Task NonAcct_Resource_SelfHrefIsTheResourceItself()
    {
        var (controller, _) = Build();
        var url = "https://example.com/communities/test";

        var result = await controller.GetWebFinger(resource: url, rel: null) as ContentResult;

        Assert.NotNull(result);
        using var doc = JsonDocument.Parse(result.Content!);
        var links = doc.RootElement.GetProperty("links");
        var self = links.EnumerateArray().First(l => l.GetProperty("rel").GetString() == "self");

        // A non-acct: resource is not rewritten to a /users/ profile; the
        // original URL is returned as the self href.
        Assert.Equal(url, self.GetProperty("href").GetString());
    }

    [Fact]
    public async Task IncludesObservatoryLinks()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: "acct:alice@example.com", rel: null) as ContentResult;
        using var doc = JsonDocument.Parse(result!.Content);
        var rels = doc.RootElement.GetProperty("links").EnumerateArray()
            .Select(l => l.GetProperty("rel").GetString())
            .ToHashSet();

        Assert.Contains("self", rels);
        Assert.Contains("http://activitypub.com/rel/inbox", rels);
        Assert.Contains("http://webfinger.net/rel/profile-page", rels);
        Assert.Contains("oauth-authorization", rels);
        Assert.Contains("http://openid.net/specs/connect/1.0/issuer", rels);
    }

    [Fact]
    public async Task InboxLink_UsesRequestSchemeAndHost()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: "acct:alice@example.com", rel: null) as ContentResult;
        using var doc = JsonDocument.Parse(result!.Content);
        var inbox = doc.RootElement.GetProperty("links").EnumerateArray()
            .First(l => l.GetProperty("rel").GetString() == "http://activitypub.com/rel/inbox");

        Assert.Equal("https://example.com/users/inbox", inbox.GetProperty("href").GetString());
    }

    [Fact]
    public async Task HttpScheme_PropagatesToLinks()
    {
        // Regression guard: when the request arrives over plain http (i.e. the
        // reverse proxy has NOT forwarded the original proto), the links must
        // use http:// — they must NOT be force-rewritten to https://.
        var (controller, _) = Build(scheme: "http");

        var result = await controller.GetWebFinger(resource: "acct:alice@example.com", rel: null) as ContentResult;
        using var doc = JsonDocument.Parse(result!.Content);
        var self = doc.RootElement.GetProperty("links").EnumerateArray()
            .First(l => l.GetProperty("rel").GetString() == "self");

        Assert.Equal("http://example.com/users/alice", self.GetProperty("href").GetString());
    }

    [Fact]
    public async Task Response_Is_CachedAndServedFromCache()
    {
        var (controller, cache) = Build();

        await controller.GetWebFinger(resource: "acct:alice@example.com", rel: null);
        await controller.GetWebFinger(resource: "acct:alice@example.com", rel: null);

        // The cache key is "{resource}:{rel}".
        var cached = cache.GetCachedResponse("acct:alice@example.com:");
        Assert.NotNull(cached);
        Assert.Equal("acct:alice@example.com", cached.Subject);
        Assert.Equal(5, cached.Links.Length);
    }
}
