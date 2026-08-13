using ActivityPub.Core.Models;

namespace ActivityPub.Core.Services;

public interface IActivityCacheService
{
    Task<Activity?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, Activity activity, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public class ActivityCacheService : IActivityCacheService
{
    private readonly Dictionary<string, Activity> _cache;
    private readonly TimeSpan _defaultSlidingExpiration;

    public ActivityCacheService()
    {
        _cache = new Dictionary<string, Activity>();
        _defaultSlidingExpiration = TimeSpan.FromMinutes(10);
    }

    public async Task<Activity?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (_cache.TryGetValue(key, out var activity))
        {
            return activity;
        }

        return null;
    }

    public async Task SetAsync(string key, Activity activity, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key) || activity == null)
            return;

        _cache[key] = activity;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            return;

        _cache.Remove(key);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
    }

    public long GetCount()
    {
        return _cache.Count;
    }
}
