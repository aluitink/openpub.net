using ActivityPub.Core.Caching;
using ActivityPub.Core.Models;

namespace ActivityPub.Core.Services;

public interface IActivityCacheService
{
    Task<Activity?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, Activity activity, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
    long GetCount();
}

public class ActivityCacheService : IActivityCacheService
{
    private readonly IFederationCache _cache;

    public ActivityCacheService(IFederationCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<Activity?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        var activity = await _cache.GetActivityAsync(key);
        return activity;
    }

    public async Task SetAsync(string key, Activity activity, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key) || activity == null)
            return;

        await _cache.SetActivityAsync(key, activity);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            return;

        await _cache.RemoveActivityAsync(key);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _cache.ClearAsync();
    }

    public long GetCount()
    {
        return _cache.Count;
    }
}
