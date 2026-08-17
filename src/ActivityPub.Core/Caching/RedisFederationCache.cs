using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace ActivityPub.Core.Caching;

/// <summary>
/// Redis-backed implementation of <see cref="IFederationCache"/> for use in
/// multi-instance deployments. All cached values are JSON-serialized and stored
/// under a configurable key prefix. Domain/actor invalidation is supported via
/// Redis Sets that track which cache keys belong to each domain or actor.
/// </summary>
public class RedisFederationCache : IFederationCache, IDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _db;
    private readonly string _prefix;
    private readonly ILogger<RedisFederationCache>? _logger;

    // Cache TTL settings (mirror MemoryFederationCache)
    private static readonly TimeSpan ActorCacheTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ActivityCacheTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WebFingerCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InboxResponseCacheTtl = TimeSpan.FromMinutes(5);

    // Key prefixes for the different cache regions
    private const string ActorKeyPrefix = "actor:";
    private const string ActivityKeyPrefix = "activity:";
    private const string WebFingerKeyPrefix = "webfinger:";
    private const string InboxKeyPrefix = "inbox:";

    // Index set key prefixes (track which keys belong to which domain/actor)
    private const string ActorDomainIndexPrefix = "idx:actor:domain:";
    private const string ActivityActorIndexPrefix = "idx:activity:actor:";
    private const string WebFingerDomainIndexPrefix = "idx:webfinger:domain:";
    private const string InboxActorIndexPrefix = "idx:inbox:actor:";

    public RedisFederationCache(
        IConnectionMultiplexer connection,
        IOptions<ActivityPubOptions> options,
        ILogger<RedisFederationCache>? logger = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _db = connection.GetDatabase();
        _prefix = options?.Value?.Cache?.CachePrefix ?? "activitypub:";
        _logger = logger;
    }

    private string FullKey(string region, string key) => $"{_prefix}{region}{key}";

    private static string ExtractDomain(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return string.Empty;
        var idx = uri.IndexOf("://", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return string.Empty;
        var rest = uri[(idx + 3)..];
        var end = rest.IndexOfAny(new[] { '/', '?', '#' });
        return end < 0 ? rest : rest[..end];
    }

    #region Actor Caching

    public async Task<Actor?> GetActorAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return null;
        try
        {
            var data = await _db.StringGetAsync(FullKey(ActorKeyPrefix, uri));
            if (data.IsNull) return null;
            return JsonSerializer.Deserialize<Actor>(data.ToString());
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis GetActorAsync failed for {Uri}", uri);
            return null;
        }
    }

    public async Task SetActorAsync(string uri, Actor actor)
    {
        if (string.IsNullOrEmpty(uri) || actor == null) return;
        try
        {
            var json = JsonSerializer.Serialize(actor);
            var key = FullKey(ActorKeyPrefix, uri);
            await _db.StringSetAsync(key, json, ActorCacheTtl);

            // Track in domain index for invalidation
            var domain = ExtractDomain(uri);
            if (!string.IsNullOrEmpty(domain))
            {
                await _db.SetAddAsync(FullKey(ActorDomainIndexPrefix, domain), key);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis SetActorAsync failed for {Uri}", uri);
        }
    }

    public async Task RemoveActorAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return;
        try
        {
            var key = FullKey(ActorKeyPrefix, uri);
            await _db.KeyDeleteAsync(key);

            var domain = ExtractDomain(uri);
            if (!string.IsNullOrEmpty(domain))
            {
                await _db.SetRemoveAsync(FullKey(ActorDomainIndexPrefix, domain), key);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis RemoveActorAsync failed for {Uri}", uri);
        }
    }

    public async Task InvalidateActorsByDomainAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return;
        try
        {
            var indexKey = FullKey(ActorDomainIndexPrefix, domain);
            var keys = await _db.SetMembersAsync(indexKey);
            foreach (var key in keys)
            {
                await _db.KeyDeleteAsync(key.ToString());
            }
            await _db.KeyDeleteAsync(indexKey);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis InvalidateActorsByDomainAsync failed for {Domain}", domain);
        }
    }

    #endregion

    #region Activity Caching

    public async Task<Activity?> GetActivityAsync(string activityId)
    {
        if (string.IsNullOrEmpty(activityId)) return null;
        try
        {
            var data = await _db.StringGetAsync(FullKey(ActivityKeyPrefix, activityId));
            if (data.IsNull) return null;
            return JsonSerializer.Deserialize<Activity>(data.ToString());
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis GetActivityAsync failed for {ActivityId}", activityId);
            return null;
        }
    }

    public async Task SetActivityAsync(string activityId, Activity activity)
    {
        if (string.IsNullOrEmpty(activityId) || activity == null) return;
        try
        {
            var json = JsonSerializer.Serialize(activity);
            var key = FullKey(ActivityKeyPrefix, activityId);
            await _db.StringSetAsync(key, json, ActivityCacheTtl);

            // Track in actor index for invalidation
            var actorId = activity.ActorId;
            if (!string.IsNullOrEmpty(actorId))
            {
                await _db.SetAddAsync(FullKey(ActivityActorIndexPrefix, actorId), key);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis SetActivityAsync failed for {ActivityId}", activityId);
        }
    }

    public async Task RemoveActivityAsync(string activityId)
    {
        if (string.IsNullOrEmpty(activityId)) return;
        try
        {
            var key = FullKey(ActivityKeyPrefix, activityId);
            await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis RemoveActivityAsync failed for {ActivityId}", activityId);
        }
    }

    public async Task InvalidateActivitiesByActorAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return;
        try
        {
            var indexKey = FullKey(ActivityActorIndexPrefix, actorId);
            var keys = await _db.SetMembersAsync(indexKey);
            foreach (var key in keys)
            {
                await _db.KeyDeleteAsync(key.ToString());
            }
            await _db.KeyDeleteAsync(indexKey);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis InvalidateActivitiesByActorAsync failed for {ActorId}", actorId);
        }
    }

    #endregion

    #region WebFinger Caching

    public async Task<WebFingerResponse?> GetWebFingerResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        try
        {
            var data = await _db.StringGetAsync(FullKey(WebFingerKeyPrefix, key));
            if (data.IsNull) return null;
            return JsonSerializer.Deserialize<WebFingerResponse>(data.ToString());
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis GetWebFingerResponseAsync failed for {Key}", key);
            return null;
        }
    }

    public async Task SetWebFingerResponseAsync(string key, WebFingerResponse response)
    {
        if (string.IsNullOrEmpty(key) || response == null) return;
        try
        {
            var json = JsonSerializer.Serialize(response);
            var redisKey = FullKey(WebFingerKeyPrefix, key);
            await _db.StringSetAsync(redisKey, json, WebFingerCacheTtl);

            // Track in domain index for invalidation (the key is typically "{resource}:{rel}")
            var domain = ExtractDomain(key);
            if (!string.IsNullOrEmpty(domain))
            {
                await _db.SetAddAsync(FullKey(WebFingerDomainIndexPrefix, domain), redisKey);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis SetWebFingerResponseAsync failed for {Key}", key);
        }
    }

    public async Task RemoveWebFingerResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        try
        {
            var redisKey = FullKey(WebFingerKeyPrefix, key);
            await _db.KeyDeleteAsync(redisKey);

            var domain = ExtractDomain(key);
            if (!string.IsNullOrEmpty(domain))
            {
                await _db.SetRemoveAsync(FullKey(WebFingerDomainIndexPrefix, domain), redisKey);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis RemoveWebFingerResponseAsync failed for {Key}", key);
        }
    }

    public async Task InvalidateWebFingerByDomainAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return;
        try
        {
            var indexKey = FullKey(WebFingerDomainIndexPrefix, domain);
            var keys = await _db.SetMembersAsync(indexKey);
            foreach (var key in keys)
            {
                await _db.KeyDeleteAsync(key.ToString());
            }
            await _db.KeyDeleteAsync(indexKey);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis InvalidateWebFingerByDomainAsync failed for {Domain}", domain);
        }
    }

    #endregion

    #region Inbox Response Caching

    public async Task<string?> GetInboxResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        try
        {
            var data = await _db.StringGetAsync(FullKey(InboxKeyPrefix, key));
            if (data.IsNull) return null;
            return data.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis GetInboxResponseAsync failed for {Key}", key);
            return null;
        }
    }

    public async Task SetInboxResponseAsync(string key, string response)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(response)) return;
        try
        {
            var redisKey = FullKey(InboxKeyPrefix, key);
            await _db.StringSetAsync(redisKey, response, InboxResponseCacheTtl);

            // Track in actor index for invalidation
            var actorId = key; // inbox response keys are typically opaque; use the key itself as the actor index
            if (!string.IsNullOrEmpty(actorId))
            {
                await _db.SetAddAsync(FullKey(InboxActorIndexPrefix, actorId), redisKey);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis SetInboxResponseAsync failed for {Key}", key);
        }
    }

    public async Task RemoveInboxResponseAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        try
        {
            var redisKey = FullKey(InboxKeyPrefix, key);
            await _db.KeyDeleteAsync(redisKey);

            var actorId = key;
            if (!string.IsNullOrEmpty(actorId))
            {
                await _db.SetRemoveAsync(FullKey(InboxActorIndexPrefix, actorId), redisKey);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis RemoveInboxResponseAsync failed for {Key}", key);
        }
    }

    public async Task InvalidateInboxResponsesByActorAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return;
        try
        {
            var indexKey = FullKey(InboxActorIndexPrefix, actorId);
            var keys = await _db.SetMembersAsync(indexKey);
            foreach (var key in keys)
            {
                await _db.KeyDeleteAsync(key.ToString());
            }
            await _db.KeyDeleteAsync(indexKey);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis InvalidateInboxResponsesByActorAsync failed for {ActorId}", actorId);
        }
    }

    #endregion

    #region Cache Management

    public async Task ClearAsync()
    {
        try
        {
            // Best-effort: delete all keys with our prefix. Uses the first
            // available server endpoint.
            var endpoints = _connection.GetEndPoints();
            if (endpoints.Length == 0) return;
            var server = _connection.GetServer(endpoints[0]);
            foreach (var key in server.Keys(pattern: $"{_prefix}*"))
            {
                await _db.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Redis ClearAsync failed");
        }
    }

    public int Count
    {
        get
        {
            try
            {
                var endpoints = _connection.GetEndPoints();
                if (endpoints.Length == 0) return 0;
                var server = _connection.GetServer(endpoints[0]);
                var count = 0;
                foreach (var _ in server.Keys(pattern: $"{_prefix}*").Take(10000))
                {
                    count++;
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }
    }

    #endregion

    public void Dispose()
    {
        // ConnectionMultiplexer is typically managed by DI; don't dispose here
        // to avoid disposing a shared connection.
    }
}
