using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemoApp.Services;

public interface IActorService
{
    Task<List<ActorEntity>> GetAllActorsAsync();
    Task<ActorEntity?> GetActorByIdAsync(int id);
    Task<ActorEntity?> GetActorByUsernameAsync(string username);
    Task<ActorEntity> CreateActorAsync(string username, string publicKey);
    Task UpdateActorAsync(ActorEntity actor);
    Task DeleteActorAsync(int id);
}

public class ActorService : IActorService
{
    private readonly ActivityPubDbContext _context;
    private readonly IMemoryCache _cache;
    private const string AllActorsCacheKey = "all_actors";
    private const string ActorByIdPrefix = "actor_by_id_";
    private const string ActorByUsernamePrefix = "actor_by_username_";
    private const int CacheDurationMinutes = 5;

    public ActorService(ActivityPubDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<ActorEntity>> GetAllActorsAsync()
    {
        return await _cache.GetOrCreateAsync(AllActorsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes);
            var actors = await _context.Actors
                .AsNoTracking()
                .OrderBy(a => a.Username)
                .ToListAsync();
            return actors;
        });
    }

    public async Task<ActorEntity?> GetActorByIdAsync(int id)
    {
        var cacheKey = $"{ActorByIdPrefix}{id}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes);
            return await _context.Actors
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
        });
    }

    public async Task<ActorEntity?> GetActorByUsernameAsync(string username)
    {
        var cacheKey = $"{ActorByUsernamePrefix}{username}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes);
            return await _context.Actors
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Username == username);
        });
    }

    public async Task<ActorEntity> CreateActorAsync(string username, string publicKey)
    {
        var actor = new ActorEntity
        {
            Username = username,
            JsonData = $"{{\"publicKey\":\"{publicKey}\"}}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Actors.AddAsync(actor);
        await _context.SaveChangesAsync();

        InvalidateActorCache();
        InvalidateActorByUsernameCache(username);

        return actor;
    }

    public async Task UpdateActorAsync(ActorEntity actor)
    {
        actor.UpdatedAt = DateTime.UtcNow;
        _context.Actors.Update(actor);
        await _context.SaveChangesAsync();

        InvalidateActorCache();
        InvalidateActorByIdCache(actor.Id);
        InvalidateActorByUsernameCache(actor.Username);
    }

    public async Task DeleteActorAsync(int id)
    {
        var actor = await _context.Actors.FindAsync(id);
        if (actor != null)
        {
            _context.Actors.Remove(actor);
            await _context.SaveChangesAsync();

            InvalidateActorCache();
            InvalidateActorByIdCache(id);
        }
    }

    private void InvalidateActorCache()
    {
        _cache.Remove(AllActorsCacheKey);
    }

    private void InvalidateActorByIdCache(int id)
    {
        _cache.Remove($"{ActorByIdPrefix}{id}");
    }

    private void InvalidateActorByUsernameCache(string username)
    {
        _cache.Remove($"{ActorByUsernamePrefix}{username}");
    }
}
