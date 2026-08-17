using ActivityPub.Core;
using ActivityPub.Core.Caching;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using System.Net;
using Xunit;

namespace ActivityPub.Tests.Caching;

public class RedisFederationCacheTests
{
    private readonly Mock<IDatabase> _db;
    private readonly Mock<IConnectionMultiplexer> _connection;
    private readonly IOptions<ActivityPubOptions> _options;
    private readonly RedisFederationCache _cache;

    public RedisFederationCacheTests()
    {
        _db = new Mock<IDatabase>();
        _connection = new Mock<IConnectionMultiplexer>();
        _connection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_db.Object);
        _connection.Setup(c => c.GetEndPoints())
            .Returns(new DnsEndPoint[] { new DnsEndPoint("localhost", 6379) });

        var options = new ActivityPubOptions();
        options.Cache.Provider = CacheProvider.Redis;
        options.Cache.CachePrefix = "test:";
        _options = new OptionsWrapper<ActivityPubOptions>(options);

        _cache = new RedisFederationCache(_connection.Object, _options, null);
    }

    #region Options Tests

    [Fact]
    public void CacheOptions_Defaults()
    {
        var options = new CacheOptions();

        Assert.Equal(CacheProvider.Memory, options.Provider);
        Assert.Equal("localhost:6379", options.RedisConnection);
        Assert.Equal("activitypub:", options.CachePrefix);
    }

    [Fact]
    public void ActivityPubOptions_ContainsCache()
    {
        var options = new ActivityPubOptions();

        Assert.NotNull(options.Cache);
        Assert.Equal(CacheProvider.Memory, options.Cache.Provider);
    }

    #endregion

    #region DI Tests

    [Fact]
    public void DI_ResolvesMemoryCacheByDefault()
    {
        var services = new ServiceCollection();
        services.AddActivityPub();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var cache = scope.ServiceProvider.GetRequiredService<IFederationCache>();

        Assert.IsType<MemoryFederationCache>(cache);
    }

    [Fact]
    public void DI_ResolvesRedisCacheWhenConfigured()
    {
        var services = new ServiceCollection();

        // Register a mock connection so the factory doesn't try to connect to
        // a real Redis server.
        var mockConnection = new Mock<IConnectionMultiplexer>();
        services.AddSingleton(mockConnection.Object);

        services.AddActivityPub(options =>
        {
            options.Cache.Provider = CacheProvider.Redis;
            options.Cache.RedisConnection = "localhost:6379";
        });

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var cache = scope.ServiceProvider.GetRequiredService<IFederationCache>();

        Assert.IsType<RedisFederationCache>(cache);
    }

    #endregion

    #region Actor Caching Tests

    [Fact]
    public async Task SetActor_StoresJsonWithTtl()
    {
        var actor = new Actor
        {
            Id = "https://example.com/users/alice",
            Name = "Alice"
        };

        await _cache.SetActorAsync(actor.Id, actor);

        // Verify StringSet was called with the correct key and JSON.
        _db.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "test:actor:https://example.com/users/alice"),
            It.Is<RedisValue>(v => v.ToString().Contains("\"id\":\"https://example.com/users/alice\"")),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

        // Verify the domain index was updated
        _db.Verify(d => d.SetAddAsync(
            It.Is<RedisKey>(k => k == "test:idx:actor:domain:example.com"),
            It.Is<RedisValue>(v => v == "test:actor:https://example.com/users/alice"),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActor_ReturnsNullOnMiss()
    {
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _cache.GetActorAsync("https://example.com/users/alice");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActor_DeserializesCachedActor()
    {
        var json = """{"id":"https://example.com/users/alice","name":"Alice"}""";
        _db.Setup(d => d.StringGetAsync("test:actor:https://example.com/users/alice"))
            .ReturnsAsync((RedisValue)json);

        var result = await _cache.GetActorAsync("https://example.com/users/alice");

        Assert.NotNull(result);
        Assert.Equal("https://example.com/users/alice", result.Id);
        Assert.Equal("Alice", result.Name);
    }

    [Fact]
    public async Task RemoveActor_DeletesKeyAndIndex()
    {
        await _cache.RemoveActorAsync("https://example.com/users/alice");

        _db.Verify(d => d.KeyDeleteAsync("test:actor:https://example.com/users/alice"), Times.Once);
        _db.Verify(d => d.SetRemoveAsync(
            It.Is<RedisKey>(k => k == "test:idx:actor:domain:example.com"),
            It.Is<RedisValue>(v => v == "test:actor:https://example.com/users/alice"),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateActorsByDomain_DeletesAllDomainKeys()
    {
        var indexKey = "test:idx:actor:domain:example.com";
        var keys = new RedisValue[]
        {
            "test:actor:https://example.com/users/alice",
            "test:actor:https://example.com/users/bob"
        };
        _db.Setup(d => d.SetMembersAsync(indexKey)).ReturnsAsync(keys);

        await _cache.InvalidateActorsByDomainAsync("example.com");

        _db.Verify(d => d.KeyDeleteAsync("test:actor:https://example.com/users/alice"), Times.Once);
        _db.Verify(d => d.KeyDeleteAsync("test:actor:https://example.com/users/bob"), Times.Once);
        _db.Verify(d => d.KeyDeleteAsync(indexKey), Times.Once);
    }

    [Fact]
    public async Task SetActor_NullActor_DoesNothing()
    {
        await _cache.SetActorAsync("https://example.com/users/alice", null!);

        _db.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    #endregion

    #region Activity Caching Tests

    [Fact]
    public async Task SetActivity_StoresJsonWithTtl()
    {
        var activity = new Activity
        {
            Id = "https://example.com/activity/1",
            Type = "Create",
            Actor = "https://example.com/users/alice"
        };

        await _cache.SetActivityAsync(activity.Id, activity);

        _db.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "test:activity:https://example.com/activity/1"),
            It.Is<RedisValue>(v => v.ToString().Contains("\"id\":\"https://example.com/activity/1\"")),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

        _db.Verify(d => d.SetAddAsync(
            It.Is<RedisKey>(k => k == "test:idx:activity:actor:https://example.com/users/alice"),
            It.Is<RedisValue>(v => v == "test:activity:https://example.com/activity/1"),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActivity_ReturnsNullOnMiss()
    {
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _cache.GetActivityAsync("https://example.com/activity/1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActivity_DeserializesCachedActivity()
    {
        var json = """{"id":"https://example.com/activity/1","type":"Create"}""";
        _db.Setup(d => d.StringGetAsync("test:activity:https://example.com/activity/1"))
            .ReturnsAsync((RedisValue)json);

        var result = await _cache.GetActivityAsync("https://example.com/activity/1");

        Assert.NotNull(result);
        Assert.Equal("https://example.com/activity/1", result.Id);
        Assert.Equal("Create", result.Type);
    }

    [Fact]
    public async Task InvalidateActivitiesByActor_DeletesAllActorKeys()
    {
        var indexKey = "test:idx:activity:actor:https://example.com/users/alice";
        var keys = new RedisValue[]
        {
            "test:activity:https://example.com/activity/1",
            "test:activity:https://example.com/activity/2"
        };
        _db.Setup(d => d.SetMembersAsync(indexKey)).ReturnsAsync(keys);

        await _cache.InvalidateActivitiesByActorAsync("https://example.com/users/alice");

        _db.Verify(d => d.KeyDeleteAsync("test:activity:https://example.com/activity/1"), Times.Once);
        _db.Verify(d => d.KeyDeleteAsync("test:activity:https://example.com/activity/2"), Times.Once);
        _db.Verify(d => d.KeyDeleteAsync(indexKey), Times.Once);
    }

    #endregion

    #region WebFinger Caching Tests

    [Fact]
    public async Task SetWebFinger_StoresJsonWithTtl()
    {
        var response = new WebFingerResponse
        {
            Subject = "acct:alice@example.com"
        };

        await _cache.SetWebFingerResponseAsync("example.com:alice", response);

        _db.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "test:webfinger:example.com:alice"),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetWebFinger_ReturnsNullOnMiss()
    {
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _cache.GetWebFingerResponseAsync("example.com:alice");

        Assert.Null(result);
    }

    #endregion

    #region Inbox Response Caching Tests

    [Fact]
    public async Task SetInboxResponse_StoresStringWithTtl()
    {
        await _cache.SetInboxResponseAsync("inbox-key-1", "{\"success\":true}");

        _db.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "test:inbox:inbox-key-1"),
            It.Is<RedisValue>(v => v == "{\"success\":true}"),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetInboxResponse_ReturnsNullOnMiss()
    {
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _cache.GetInboxResponseAsync("inbox-key-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetInboxResponse_ReturnsCachedString()
    {
        _db.Setup(d => d.StringGetAsync("test:inbox:inbox-key-1"))
            .ReturnsAsync((RedisValue)"{\"success\":true}");

        var result = await _cache.GetInboxResponseAsync("inbox-key-1");

        Assert.Equal("{\"success\":true}", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetActor_EmptyUri_ReturnsNull()
    {
        var result = await _cache.GetActorAsync(string.Empty);

        Assert.Null(result);
        _db.Verify(d => d.StringGetAsync(It.IsAny<RedisKey>()), Times.Never);
    }

    [Fact]
    public async Task SetActor_EmptyUri_DoesNothing()
    {
        var actor = new Actor { Id = "https://example.com/users/alice" };
        await _cache.SetActorAsync(string.Empty, actor);

        _db.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task InvalidateActorsByDomain_EmptyDomain_DoesNothing()
    {
        await _cache.InvalidateActorsByDomainAsync(string.Empty);

        _db.Verify(d => d.SetMembersAsync(It.IsAny<RedisKey>()), Times.Never);
    }

    #endregion
}
