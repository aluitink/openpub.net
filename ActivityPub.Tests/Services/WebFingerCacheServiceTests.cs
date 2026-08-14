using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace ActivityPub.Tests.Services;

public class WebFingerCacheServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly WebFingerCacheService _service;

    public WebFingerCacheServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new WebFingerCacheService(_cache);
    }

    [Fact]
    public void GetCachedResponse_CacheEmpty_ReturnsNull()
    {
        var result = _service.GetCachedResponse("nonexistent.key");

        Assert.Null(result);
    }

    [Fact]
    public void SetCachedResponse_CanRetrieveValue()
    {
        var response = new WebFingerResponse
        {
            Subject = "acct:test@example.com",
            Links = new WebFingerLink[]
            {
                new WebFingerLink { Rel = "self", Href = "https://example.com/users/test" }
            }
        };

        _service.SetCachedResponse("test.key", response);

        var result = _service.GetCachedResponse("test.key");

        Assert.NotNull(result);
        Assert.Equal("acct:test@example.com", result.Subject);
        Assert.Single(result.Links);
    }

    [Fact]
    public void SetCachedResponse_OverwritesExisting()
    {
        var response1 = new WebFingerResponse
        {
            Subject = "acct:test@example.com",
            Links = new WebFingerLink[] { new WebFingerLink { Rel = "self", Href = "https://example.com/users/test" } }
        };

        var response2 = new WebFingerResponse
        {
            Subject = "acct:updated@example.com",
            Links = new WebFingerLink[] { new WebFingerLink { Rel = "self", Href = "https://example.com/users/updated" } }
        };

        _service.SetCachedResponse("test.key", response1);
        _service.SetCachedResponse("test.key", response2);

        var result = _service.GetCachedResponse("test.key");

        Assert.NotNull(result);
        Assert.Equal("acct:updated@example.com", result.Subject);
    }

    [Fact]
    public void GetCachedResponse_MultipleKeys_Isolated()
    {
        var response1 = new WebFingerResponse { Subject = "acct:key1@example.com", Links = Array.Empty<WebFingerLink>() };
        var response2 = new WebFingerResponse { Subject = "acct:key2@example.com", Links = Array.Empty<WebFingerLink>() };

        _service.SetCachedResponse("key1", response1);
        _service.SetCachedResponse("key2", response2);

        var result1 = _service.GetCachedResponse("key1");
        var result2 = _service.GetCachedResponse("key2");

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("acct:key1@example.com", result1!.Subject);
        Assert.Equal("acct:key2@example.com", result2!.Subject);
    }

}
