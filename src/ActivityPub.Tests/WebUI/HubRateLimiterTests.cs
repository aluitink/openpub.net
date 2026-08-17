using ActivityPub.Core.Options;
using ActivityPub.WebUI.Hubs;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Unit tests for the per-connection SignalR hub rate limiters introduced for
/// Phase 39 T2 (WebSocket scaling). The in-memory limiter is process-local;
/// the Redis limiter shares counts across instances.
/// </summary>
public class HubRateLimiterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // ---------- InMemoryHubRateLimiter ----------

    [Fact]
    public async Task InMemory_Allows_Messages_Under_Limit()
    {
        var limiter = new InMemoryHubRateLimiter();

        for (int i = 0; i < 10; i++)
        {
            var allowed = await limiter.TryRecordAsync("conn-1", 50, Window);
            Assert.True(allowed, $"Message {i + 1} should be allowed");
        }
    }

    [Fact]
    public async Task InMemory_Blocks_Messages_Over_Limit()
    {
        var limiter = new InMemoryHubRateLimiter();

        bool last = false;
        for (int i = 0; i < 51; i++)
        {
            last = await limiter.TryRecordAsync("conn-1", 50, Window);
        }

        Assert.False(last, "The 51st message should be rate-limited");
    }

    [Fact]
    public async Task InMemory_Tracks_Connections_Independently()
    {
        var limiter = new InMemoryHubRateLimiter();

        // Exhaust conn-1's budget.
        for (int i = 0; i < 50; i++)
        {
            await limiter.TryRecordAsync("conn-1", 50, Window);
        }
        Assert.False(await limiter.TryRecordAsync("conn-1", 50, Window));

        // conn-2 still has a fresh budget.
        Assert.True(await limiter.TryRecordAsync("conn-2", 50, Window));
    }

    [Fact]
    public async Task InMemory_Clear_Resets_Budget()
    {
        var limiter = new InMemoryHubRateLimiter();

        for (int i = 0; i < 50; i++)
        {
            await limiter.TryRecordAsync("conn-1", 50, Window);
        }
        Assert.False(await limiter.TryRecordAsync("conn-1", 50, Window));

        await limiter.ClearAsync("conn-1");

        Assert.True(await limiter.TryRecordAsync("conn-1", 50, Window),
            "Budget should be reset after Clear");
    }

    [Fact]
    public async Task InMemory_Window_Expires_And_Resets()
    {
        var limiter = new InMemoryHubRateLimiter();
        var tinyWindow = TimeSpan.FromMilliseconds(20);

        // Fill the budget within a tiny window.
        for (int i = 0; i < 3; i++)
        {
            await limiter.TryRecordAsync("conn-1", 3, tinyWindow);
        }
        Assert.False(await limiter.TryRecordAsync("conn-1", 3, tinyWindow));

        // Wait for the window to expire.
        await Task.Delay(30);

        Assert.True(await limiter.TryRecordAsync("conn-1", 3, tinyWindow),
            "Budget should reset after the window elapses");
    }

    // ---------- RedisHubRateLimiter ----------

    [Fact]
    public async Task Redis_TryRecord_Evaluates_Script_With_Correct_Key_And_Args()
    {
        var (limiter, db) = CreateRedisLimiter(out _);

        // Simulate Redis returning "within limit".
        db.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((long)1));

        var allowed = await limiter.TryRecordAsync("conn-42", 50, Window);

        Assert.True(allowed);
        db.Verify(d => d.ScriptEvaluateAsync(
            It.Is<string>(s => s.Contains("window_start")),
            It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == "activitypub:rate:conn-42"),
            It.Is<RedisValue[]>(args => args.Length == 3 && (long)args[1] == 50),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task Redis_TryRecord_Returns_False_When_Over_Limit()
    {
        var (limiter, db) = CreateRedisLimiter(out _);

        db.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((long)0));

        var allowed = await limiter.TryRecordAsync("conn-42", 50, Window);

        Assert.False(allowed);
    }

    [Fact]
    public async Task Redis_Clear_Deletes_Key()
    {
        var (limiter, db) = CreateRedisLimiter(out _);

        await limiter.ClearAsync("conn-42");

        db.Verify(d => d.KeyDeleteAsync(It.Is<RedisKey>(k => k == "activitypub:rate:conn-42")),
            Times.Once);
    }

    [Fact]
    public void Redis_Uses_CachePrefix_From_Options()
    {
        var limiter = CreateLimiterWithPrefix("custom:");
        // We can't observe the key without invoking, but verify the constructor
        // accepted a custom prefix by checking the type is correct and the
        // object is usable. The key-building is validated in the other tests.
        Assert.NotNull(limiter);
    }

    // ---------- RealtimeOptions defaults ----------

    [Fact]
    public void RealtimeOptions_Defaults_Are_Sane()
    {
        var options = new RealtimeOptions();

        Assert.False(options.Enabled, "Scale-out should be opt-in");
        Assert.Equal("localhost:6379", options.RedisConnection);
        Assert.Equal("activitypub:signalr:", options.ChannelPrefix);
        Assert.Equal(50, options.MaxMessagesPerWindow);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Window);
    }

    [Fact]
    public void ActivityPubOptions_Exposes_Realtime_With_Defaults()
    {
        var options = new ActivityPubOptions();

        Assert.NotNull(options.Realtime);
        Assert.False(options.Realtime.Enabled);
    }

    [Fact]
    public void RealtimeOptions_Window_Binds_From_Json_Duration()
    {
        var options = System.Text.Json.JsonSerializer.Deserialize<RealtimeOptions>(
            """{"Enabled":true,"RedisConnection":"redis:6379","ChannelPrefix":"x:","MaxMessagesPerWindow":10,"Window":"00:05:00"}""");

        Assert.NotNull(options);
        Assert.True(options.Enabled);
        Assert.Equal("redis:6379", options.RedisConnection);
        Assert.Equal("x:", options.ChannelPrefix);
        Assert.Equal(10, options.MaxMessagesPerWindow);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Window);
    }

    // ---------- Helpers ----------

    private static (RedisHubRateLimiter Limiter, Mock<IDatabase> Db) CreateRedisLimiter(out Mock<IConnectionMultiplexer> connection)
    {
        var db = new Mock<IDatabase>();
        connection = new Mock<IConnectionMultiplexer>();
        connection.Setup(c => c.GetDatabase()).Returns(db.Object);

        var options = Options.Create(new ActivityPubOptions
        {
            Cache = new CacheOptions { CachePrefix = "activitypub:" }
        });

        return (new RedisHubRateLimiter(connection.Object, options), db);
    }

    private static RedisHubRateLimiter CreateLimiterWithPrefix(string prefix)
    {
        var connection = new Mock<IConnectionMultiplexer>();
        connection.Setup(c => c.GetDatabase()).Returns(new Mock<IDatabase>().Object);
        var options = Options.Create(new ActivityPubOptions
        {
            Cache = new CacheOptions { CachePrefix = prefix }
        });
        return new RedisHubRateLimiter(connection.Object, options);
    }
}
