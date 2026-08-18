using ActivityPub.Core.Controllers.Versioned;
using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="WebFingerVersionedController"/> — the versioned
/// WebFinger controller, which previously had no direct unit test. Drives the
/// controller with a real <see cref="WebFingerCacheService"/> (over an
/// in-memory cache) and asserts on the returned <see cref="ContentResult"/>
/// body.
/// </summary>
public class WebFingerVersionedControllerTests
{
    private static (WebFingerVersionedController controller, WebFingerCacheService cache) Build()
    {
        var cache = new WebFingerCacheService(new MemoryCache(new MemoryCacheOptions()));
        var controller = new WebFingerVersionedController(cache);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        return (controller, cache);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task MissingResource_Returns400()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: null, rel: null);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(400, content.StatusCode);
        Assert.Equal("application/json", content.ContentType);

        var json = Parse(content.Content!);
        Assert.Equal("Missing required resource parameter", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task EmptyResource_Returns400()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: "   ", rel: null);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(400, content.StatusCode);
    }

    [Fact]
    public async Task AcctResource_ReturnsSelfLinkWithActivityType()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: "acct:alice@example.com", rel: "self");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/jrd+json", content.ContentType);

        var json = Parse(content.Content!);
        Assert.Equal("acct:alice@example.com", json.GetProperty("subject").GetString());

        var links = json.GetProperty("links");
        Assert.Equal(1, links.GetArrayLength());
        var self = links[0];
        Assert.Equal("self", self.GetProperty("rel").GetString());
        Assert.Equal("application/activity+json", self.GetProperty("type").GetString());
        Assert.Equal("/users/alice", self.GetProperty("href").GetString());
    }

    [Fact]
    public async Task NonAcctResource_UsesResourceAsHref()
    {
        var (controller, _) = Build();

        var result = await controller.GetWebFinger(resource: "https://example.com/@bob", rel: null);

        var content = Assert.IsType<ContentResult>(result);
        var json = Parse(content.Content!);

        Assert.Equal("https://example.com/@bob", json.GetProperty("subject").GetString());
        var self = json.GetProperty("links")[0];
        Assert.Equal("https://example.com/@bob", self.GetProperty("href").GetString());
    }

    [Fact]
    public async Task SecondCall_ServedFromCache()
    {
        var (controller, cache) = Build();

        // Prime the cache.
        await controller.GetWebFinger(resource: "acct:carol@example.com", rel: "self");

        // The cache should now hold the response for that key.
        var cached = cache.GetCachedResponse("acct:carol@example.com:self");
        Assert.NotNull(cached);
        Assert.Equal("acct:carol@example.com", cached!.Subject);

        // A second request for the same key is served from the cache (same shape).
        var result = await controller.GetWebFinger(resource: "acct:carol@example.com", rel: "self");
        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/jrd+json", content.ContentType);
        var json = Parse(content.Content!);
        Assert.Equal("/users/carol", json.GetProperty("links")[0].GetProperty("href").GetString());
    }
}
