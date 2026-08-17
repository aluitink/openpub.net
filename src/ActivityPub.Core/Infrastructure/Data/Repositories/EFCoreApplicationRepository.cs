using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ActivityPub.Core.Implementations;

/// <summary>
/// EF Core implementation of <see cref="IApplicationRepository"/> backed by
/// <see cref="ActivityPubDbContext.OAuthClients"/>.
/// </summary>
public class EFCoreApplicationRepository : IApplicationRepository
{
    private readonly ActivityPubDbContext _context;

    public EFCoreApplicationRepository(ActivityPubDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveApplicationAsync(OAuthClientEntity client)
    {
        if (client == null || string.IsNullOrWhiteSpace(client.ClientId))
            return false;

        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<OAuthClientEntity?> GetApplicationAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return null;

        return await _context.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId);
    }

    public async Task<bool> VerifyClientAsync(string clientId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrEmpty(clientSecret))
            return false;

        var client = await _context.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId);

        if (client == null)
            return false;

        return client.ClientSecret == clientSecret;
    }

    public async Task<IReadOnlyList<OAuthClientEntity>> GetAllAsync()
    {
        return await _context.OAuthClients.ToListAsync();
    }

    public async Task<IReadOnlyList<OAuthClientEntity>> GetByOwnerAsync(string ownerActorId)
    {
        if (string.IsNullOrWhiteSpace(ownerActorId))
            return Array.Empty<OAuthClientEntity>();

        return await _context.OAuthClients
            .Where(c => c.OwnerActorId == ownerActorId)
            .ToListAsync();
    }
}
