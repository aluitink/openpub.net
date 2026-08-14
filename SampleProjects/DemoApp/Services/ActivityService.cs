using ActivityPub.Core.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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

    public ActivityService(ActivityPubDbContext context, IHubContext<ActivityHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<List<ActivityEntity>> GetAllActivitiesAsync()
    {
        return await _context.Activities.ToListAsync();
    }

    public async Task<List<ActivityEntity>> GetPaginatedActivitiesAsync(int page, int pageSize)
    {
        return await _context.Activities
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalActivitiesCountAsync()
    {
        return await _context.Activities.CountAsync();
    }

    public async Task<ActivityEntity?> GetActivityByIdAsync(int id)
    {
        return await _context.Activities.FindAsync(id);
    }

    public async Task<ActivityEntity?> GetActivityByActivityIdAsync(string activityId)
    {
        return await _context.Activities.FirstOrDefaultAsync(a => a.ActivityId == activityId);
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

        return activity;
    }

    public async Task UpdateActivityAsync(ActivityEntity activity)
    {
        activity.UpdatedAt = DateTime.UtcNow;
        _context.Activities.Update(activity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteActivityAsync(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity != null)
        {
            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task BroadcastActivityAsync(string jsonData)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveActivity", jsonData);
    }
}
