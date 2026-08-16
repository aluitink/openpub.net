using System.Text.Json;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ActivityPub.Core.Services;

public class CommunityServiceImpl : ICommunityService
{
    private readonly ActivityPubDbContext _context;

    public CommunityServiceImpl(ActivityPubDbContext context)
    {
        _context = context;
    }

    public async Task<Community?> CreateCommunityAsync(string ownerId, string name, string? summary, CancellationToken cancellationToken = default)
    {
        var owner = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{ownerId}\""), cancellationToken);

        if (owner == null) return null;

        var communityId = $"{ExtractServer(owner.JsonData)}/communities/{name.ToLowerInvariant().Replace(" ", "-")}";

        var community = new Community
        {
            Id = communityId,
            Type = "Group",
            Name = name,
            Summary = summary,
            Published = DateTime.UtcNow,
            OwnerId = ownerId,
            ManuallyApprovesFollowers = false
        };

        var jsonData = JsonSerializer.Serialize(community, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var entity = new CommunityEntity
        {
            CommunityId = communityId,
            Name = name,
            JsonData = jsonData,
            OwnerActorId = owner.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Communities.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        var member = new CommunityMemberEntity
        {
            CommunityId = entity.Id,
            ActorId = owner.Id,
            JoinedAt = DateTime.UtcNow
        };
        _context.CommunityMembers.Add(member);
        await _context.SaveChangesAsync(cancellationToken);

        return community;
    }

    public async Task<Community?> GetCommunityByIdAsync(string communityId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Communities
            .FirstOrDefaultAsync(c => c.CommunityId == communityId, cancellationToken);

        if (entity == null) return null;

        return JsonSerializer.Deserialize<Community>(entity.JsonData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public async Task<ICollection<Community>> GetAllCommunitiesAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Communities
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(e =>
            JsonSerializer.Deserialize<Community>(e.JsonData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!)
            .ToList();
    }

    public async Task<bool> JoinCommunityAsync(string actorId, string communityId, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{actorId}\""), cancellationToken);
        if (actor == null) return false;

        var community = await _context.Communities
            .FirstOrDefaultAsync(c => c.CommunityId == communityId, cancellationToken);
        if (community == null) return false;

        var exists = await _context.CommunityMembers
            .AnyAsync(m => m.CommunityId == community.Id && m.ActorId == actor.Id, cancellationToken);
        if (exists) return true;

        var member = new CommunityMemberEntity
        {
            CommunityId = community.Id,
            ActorId = actor.Id,
            JoinedAt = DateTime.UtcNow
        };

        _context.CommunityMembers.Add(member);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> LeaveCommunityAsync(string actorId, string communityId, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{actorId}\""), cancellationToken);
        if (actor == null) return false;

        var community = await _context.Communities
            .FirstOrDefaultAsync(c => c.CommunityId == communityId, cancellationToken);
        if (community == null) return false;

        var member = await _context.CommunityMembers
            .FirstOrDefaultAsync(m => m.CommunityId == community.Id && m.ActorId == actor.Id, cancellationToken);
        if (member == null) return false;

        _context.CommunityMembers.Remove(member);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> IsMemberAsync(string actorId, string communityId, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{actorId}\""), cancellationToken);
        if (actor == null) return false;

        var community = await _context.Communities
            .FirstOrDefaultAsync(c => c.CommunityId == communityId, cancellationToken);
        if (community == null) return false;

        return await _context.CommunityMembers
            .AnyAsync(m => m.CommunityId == community.Id && m.ActorId == actor.Id, cancellationToken);
    }

    public async Task<ICollection<string>> GetMemberIdsAsync(string communityId, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .FirstOrDefaultAsync(c => c.CommunityId == communityId, cancellationToken);
        if (community == null) return new List<string>();

        var memberActorIds = await _context.CommunityMembers
            .Where(m => m.CommunityId == community.Id)
            .Select(m => m.ActorId)
            .ToListAsync(cancellationToken);

        var actors = await _context.Actors
            .Where(a => memberActorIds.Contains(a.Id))
            .Select(a => a.JsonData)
            .ToListAsync(cancellationToken);

        return actors.Select(a => ExtractId(a)).ToList();
    }

    public async Task<int> GetMemberCountAsync(string communityId, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .FirstOrDefaultAsync(c => c.CommunityId == communityId, cancellationToken);
        if (community == null) return 0;

        return await _context.CommunityMembers
            .CountAsync(m => m.CommunityId == community.Id, cancellationToken);
    }

    public async Task<ICollection<Community>> GetMyCommunitiesAsync(string actorId, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{actorId}\""), cancellationToken);
        if (actor == null) return new List<Community>();

        var communityIds = await _context.CommunityMembers
            .Where(m => m.ActorId == actor.Id)
            .Select(m => m.CommunityId)
            .ToListAsync(cancellationToken);

        if (communityIds.Count == 0) return new List<Community>();

        var entities = await _context.Communities
            .Where(c => communityIds.Contains(c.Id))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e =>
            JsonSerializer.Deserialize<Community>(e.JsonData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!)
            .ToList();
    }

    public async Task<ICollection<Community>> SearchCommunitiesAsync(string query, CancellationToken cancellationToken = default)
    {
        var lowerQuery = query.ToLowerInvariant();

        var entities = await _context.Communities
            .ToListAsync(cancellationToken);

        return entities
            .Where(e => e.Name.ToLowerInvariant().Contains(lowerQuery))
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .Select(e =>
                JsonSerializer.Deserialize<Community>(e.JsonData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!)
            .ToList();
    }

    public async Task<bool> DeleteCommunityAsync(string ownerId, string communityId, CancellationToken cancellationToken = default)
    {
        var owner = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{ownerId}\""), cancellationToken);
        if (owner == null) return false;

        var community = await _context.Communities
            .FirstOrDefaultAsync(c => c.CommunityId == communityId && c.OwnerActorId == owner.Id, cancellationToken);
        if (community == null) return false;

        var members = await _context.CommunityMembers
            .Where(m => m.CommunityId == community.Id)
            .ToListAsync(cancellationToken);

        _context.CommunityMembers.RemoveRange(members);
        _context.Communities.Remove(community);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string ExtractId(string jsonData)
    {
        var idx = jsonData.IndexOf("\"id\":\"");
        if (idx < 0) return string.Empty;
        var start = idx + 6;
        var end = jsonData.IndexOf("\"", start);
        return end > start ? jsonData.Substring(start, end - start) : string.Empty;
    }

    private static string ExtractServer(string jsonData)
    {
        var idIdx = jsonData.IndexOf("\"id\":\"");
        if (idIdx < 0) return "https://localhost";
        var idStart = idIdx + 6;
        var idEnd = jsonData.IndexOf("\"", idStart);
        var id = idEnd > idStart ? jsonData.Substring(idStart, idEnd - idStart) : "https://localhost/";

        var uriBuilder = new UriBuilder(id);
        return $"{uriBuilder.Scheme}://{uriBuilder.Host}{(uriBuilder.Port != -1 && uriBuilder.Port != 80 && uriBuilder.Port != 443 ? $":{uriBuilder.Port}" : "")}";
    }
}
