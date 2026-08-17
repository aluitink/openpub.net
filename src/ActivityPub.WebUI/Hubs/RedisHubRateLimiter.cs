using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ActivityPub.Core.Options;

namespace ActivityPub.WebUI.Hubs;

/// <summary>
/// Distributed rate limiter backed by Redis. Uses an atomic Lua script so that
/// the window-reset + increment + check happens server-side without races. A
/// single <see cref="IConnectionMultiplexer"/> is shared across all instances
/// (typically the same one used by the SignalR backplane), so a connection's
/// message budget is enforced globally no matter which instance it lands on.
///
/// Keys are namespaced with <c>rate:&lt;connectionId&gt;</c> and carry a TTL of
/// twice the window so that idle keys expire automatically.
/// </summary>
public class RedisHubRateLimiter : IHubRateLimiter
{
    private const string KeyPrefix = "rate:";

    /// <summary>
    /// Atomically: if the key is missing or older than the window, reset the
    /// window start and set the count to 1. Otherwise increment the existing
    /// count. Returns 1 if the new count is within the limit, 0 otherwise.
    /// </summary>
    private const string RateLimitScript = """
        local key = KEYS[1]
        local windowMs = tonumber(ARGV[1])
        local maxMessages = tonumber(ARGV[2])
        local nowMs = tonumber(ARGV[3])

        local data = redis.call('HMGET', key, 'window_start', 'count')
        local windowStart = tonumber(data[1]) or 0
        local count = tonumber(data[2]) or 0

        if windowStart == 0 or (nowMs - windowStart) > windowMs then
            windowStart = nowMs
            count = 0
        end

        count = count + 1
        redis.call('HMSET', key, 'window_start', windowStart, 'count', count)
        redis.call('PEXPIRE', key, windowMs * 2)

        if count <= maxMessages then
            return 1
        else
            return 0
        end
        """;

    private readonly IConnectionMultiplexer _connection;
    private readonly string _prefix;

    public RedisHubRateLimiter(IConnectionMultiplexer connection, IOptions<ActivityPubOptions> options)
    {
        _connection = connection;
        _prefix = options.Value.Cache.CachePrefix;
    }

    public async Task<bool> TryRecordAsync(string connectionId, int maxMessages, TimeSpan window)
    {
        var db = _connection.GetDatabase();
        var key = (RedisKey)$"{_prefix}{KeyPrefix}{connectionId}";
        var nowMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        var windowMs = (long)window.TotalMilliseconds;

        var keys = new[] { key };
        var args = new RedisValue[] { windowMs, maxMessages, nowMs };
        var result = await db.ScriptEvaluateAsync(RateLimitScript, keys, args);
        return (long)result == 1;
    }

    public async Task ClearAsync(string connectionId)
    {
        var db = _connection.GetDatabase();
        var key = $"{_prefix}{KeyPrefix}{connectionId}";
        await db.KeyDeleteAsync(key);
    }
}
