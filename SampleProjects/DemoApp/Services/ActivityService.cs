using ActivityPub.Core.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemoApp.Services;

public interface IActivityService
{
    Task<List<ActivityEntity>> GetAllActivitiesAsync();
    Task<List<ActivityEntity>> GetPaginatedActivitiesAsync(int page, int pageSize);
    Task<int> GetTotalActivitiesCountAsync();
    Task<ActivityEntity?> GetActivityByIdAsync(int id);
    Task<ActivityEntity?> GetActivityByActivityIdAsync(string activityId);
    Task<ActivityEntity> CreateActivityAsync(string activityId, string jsonData);
    Task UpdateActivityAsync(ActivityEntity activity);
    Task DeleteActivityAsync(int id);
    Task BroadcastActivityAsync(string jsonData);
}

public class ActivityService : IActivityService
{
    private readonly ActivityPubDbContext _context;
    private readonly IHubContext<ActivityHub> _hubContext;
    private readonly IMemoryCache _cache;
    private const string AllActivitiesCacheKey = "all_activities";
    private const string ActivityByIdPrefix = "activity_by_id_";
    private const string ActivityByActivityIdPrefix = "activity_by_activity_id_";
    private const int CacheDurationMinutes = 5;

    public ActivityService(ActivityPubDbContext context, IHubContext<ActivityHub> hubContext, IMemoryCache cache)
    {
        _context = context;
        _hubContext = hubContext;
        _cache = cache;
    }

    public async Task<List<ActivityEntity>> GetAllActivitiesAsync()
    {
        return await _cache.GetOrCreateAsync(AllActivitiesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes);
            var activities = await _context.Activities
                .AsNoTracking()
                .OrderByDescending(a => a.Id)
                .Take(100)
                .ToListAsync();
            return activities;
        });
    }

    public async Task<List<ActivityEntity>> GetPaginatedActivitiesAsync(int page, int pageSize)
    {
        var cacheKey = $"paginated_activities_{page}_{pageSize}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes);
            return await _context.Activities
                .AsNoTracking()
                .OrderByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        });
    }

    public async Task<int> GetTotalActivitiesCountAsync()
    {
        return await _cache.GetOrCreateAsync("total_activities_count", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes);
            return await _context.Activities.CountAsync();
        });
    }

    public async Task<ActivityEntity?> GetActivityByIdAsync(int id)
    {
        var cacheKey = $"{ActivityByIdPrefix}{id}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes);
            return await _context.Activities
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
        });
    }

    public async Task<ActivityEntity?> GetActivityByActivityIdAsync(string activityId)
    {
        var cacheKey = $"{ActivityByActivityIdPrefix}{activityId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes);
            return await _context.Activities
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ActivityId == activityId);
        });
    }

    public async Task<ActivityEntity> CreateActivityAsync(string activityId, string jsonData)
    {
        var activity = new ActivityEntity
        {
            ActivityId = activityId,
            JsonData = jsonData,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Activities.AddAsync(activity);
        await _context.SaveChangesAsync();

        await BroadcastActivityAsync(jsonData);

        InvalidateActivityCache();
        InvalidateActivityByIdCache(activity.Id);
        InvalidateActivityByActivityIdCache(activityId);

        return activity;
    }

    public async Task UpdateActivityAsync(ActivityEntity activity)
    {
        activity.UpdatedAt = DateTime.UtcNow;
        _context.Activities.Update(activity);
        await _context.SaveChangesAsync();

        InvalidateActivityCache();
        InvalidateActivityByIdCache(activity.Id);
        InvalidateActivityByActivityIdCache(activity.ActivityId);
    }

    public async Task DeleteActivityAsync(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity != null)
        {
            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            InvalidateActivityCache();
            InvalidateActivityByIdCache(id);
        }
    }

    public async Task BroadcastActivityAsync(string jsonData)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveActivity", jsonData);
    }

    private void InvalidateActivityCache()
    {
        _cache.Remove(AllActivitiesCacheKey);
    }

    private void InvalidateActivityByIdCache(int id)
    {
        _cache.Remove($"{ActivityByIdPrefix}{id}");
    }

    private void InvalidateActivityByActivityIdCache(string activityId)
    {
        _cache.Remove($"{ActivityByActivityIdPrefix}{activityId}");
    }
}
