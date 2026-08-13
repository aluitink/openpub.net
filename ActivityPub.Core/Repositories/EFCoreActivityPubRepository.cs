using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ActivityPub.Core.Implementations;

public class EFCoreActivityPubRepository : IActivityPubRepository
{
    private readonly ActivityPubDbContext _context;
    private readonly JsonSerializerOptions _jsonOptions;

    public EFCoreActivityPubRepository(ActivityPubDbContext context)
    {
        _context = context;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<Actor?> GetUserActorAsync(string username)
    {
        var entity = await _context.Actors
            .Where(a => a.Username == username)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Actor>(entity.JsonData, _jsonOptions);
    }

    public async Task<bool> SaveUserActorAsync(Actor actor)
    {
        var jsonData = JsonSerializer.Serialize(actor, _jsonOptions);
        var username = GetUsernameFromActor(actor);

        var existing = await _context.Actors
            .Where(a => a.Username == username)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.JsonData = jsonData;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.Actors.Update(existing);
        }
        else
        {
            var entity = new ActorEntity
            {
                Username = username,
                JsonData = jsonData,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.Actors.AddAsync(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SaveActivityAsync(Activity activity)
    {
        var jsonData = JsonSerializer.Serialize(activity, _jsonOptions);
        
        var existing = await _context.Activities
            .Where(a => a.ActivityId == activity.Id)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.JsonData = jsonData;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.Activities.Update(existing);
        }
        else
        {
            var entity = new ActivityEntity
            {
                ActivityId = activity.Id,
                JsonData = jsonData,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.Activities.AddAsync(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Activity?> GetActivityAsync(string activityId)
    {
        var entity = await _context.Activities
            .Where(a => a.ActivityId == activityId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Activity>(entity.JsonData, _jsonOptions);
    }

    private string GetUsernameFromActor(Actor actor)
    {
        if (!string.IsNullOrEmpty(actor.PreferredUsername))
        {
            return actor.PreferredUsername;
        }

        if (!string.IsNullOrEmpty(actor.Id))
        {
            var segments = actor.Id.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[^1] : actor.Id;
        }

        return actor.Id ?? Guid.NewGuid().ToString();
    }
}
