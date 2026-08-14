using ActivityPub.Core.Caching;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

public class ActivityCacheServiceTests
{
    private readonly ILoggerFactory _loggerFactory;

    public ActivityCacheServiceTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    [Fact]
    public async Task GetAsync_RetrievesCachedActivity()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ActivityCacheService(new MemoryFederationCache(memoryCache));

        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "Test content"
            }
        };

        await cache.SetAsync("test-key", activity);

        var result = await cache.GetAsync("test-key");

        Assert.NotNull(result);
        Assert.Equal("https://example.com/activity/1", result.Id);
        Assert.Equal("Create", result.Type);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForMissingKey()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ActivityCacheService(new MemoryFederationCache(memoryCache));

        var result = await cache.GetAsync("nonexistent-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_CachesActivity()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ActivityCacheService(new MemoryFederationCache(memoryCache));

        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "Test content"
            }
        };

        await cache.SetAsync("test-key", activity);

        Assert.Equal(1, cache.GetCount());
    }

    [Fact]
    public async Task RemoveAsync_RemovesCachedActivity()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ActivityCacheService(new MemoryFederationCache(memoryCache));

        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "Test content"
            }
        };

        await cache.SetAsync("test-key", activity);
        await cache.RemoveAsync("test-key");

        var result = await cache.GetAsync("test-key");
        Assert.Null(result);
        Assert.Equal(0, cache.GetCount());
    }

    [Fact]
    public async Task ClearAsync_ClearsAllCachedActivities()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ActivityCacheService(new MemoryFederationCache(memoryCache));

        var activity1 = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "Test content 1"
            }
        };

        var activity2 = new Activity
        {
            Id = "https://example.com/activity/2",
            Type = "Like",
            Object = "https://example.com/object/2"
        };

        await cache.SetAsync("key1", activity1);
        await cache.SetAsync("key2", activity2);

        await cache.ClearAsync();

        Assert.Equal(0, cache.GetCount());
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForNullKey()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ActivityCacheService(new MemoryFederationCache(memoryCache));

        var result = await cache.GetAsync(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_IgnoresNullActivity()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ActivityCacheService(new MemoryFederationCache(memoryCache));

        await cache.SetAsync("test-key", null!);

        Assert.Equal(0, cache.GetCount());
    }

    [Fact]
    public async Task SetAsync_IgnoresNullKey()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ActivityCacheService(new MemoryFederationCache(memoryCache));

        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            Object = new ActivityPub.Core.Models.Object
            {
                Id = "https://example.com/object/1",
                Type = "Note",
                Content = "Test content"
            }
        };

        await cache.SetAsync(string.Empty, activity);

        Assert.Equal(0, cache.GetCount());
    }
}
