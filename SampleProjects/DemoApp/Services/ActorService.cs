using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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

    public ActorService(ActivityPubDbContext context)
    {
        _context = context;
    }

    public async Task<List<ActorEntity>> GetAllActorsAsync()
    {
        return await _context.Actors.ToListAsync();
    }

    public async Task<ActorEntity?> GetActorByIdAsync(int id)
    {
        return await _context.Actors.FindAsync(id);
    }

    public async Task<ActorEntity?> GetActorByUsernameAsync(string username)
    {
        return await _context.Actors.FirstOrDefaultAsync(a => a.Username == username);
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

        return actor;
    }

    public async Task UpdateActorAsync(ActorEntity actor)
    {
        actor.UpdatedAt = DateTime.UtcNow;
        _context.Actors.Update(actor);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteActorAsync(int id)
    {
        var actor = await _context.Actors.FindAsync(id);
        if (actor != null)
        {
            _context.Actors.Remove(actor);
            await _context.SaveChangesAsync();
        }
    }
}
