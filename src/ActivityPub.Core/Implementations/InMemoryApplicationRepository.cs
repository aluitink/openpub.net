using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;

namespace ActivityPub.Core.Implementations;

/// <summary>
/// In-memory implementation of <see cref="IApplicationRepository"/> for tests
/// and lightweight scenarios.
/// </summary>
public class InMemoryApplicationRepository : IApplicationRepository
{
    private readonly Dictionary<string, OAuthClientEntity> _applications = new();
    private readonly object _lock = new();

    public Task<bool> SaveApplicationAsync(OAuthClientEntity client)
    {
        if (client == null || string.IsNullOrWhiteSpace(client.ClientId))
            return Task.FromResult(false);

        lock (_lock)
        {
            _applications[client.ClientId] = client;
        }
        return Task.FromResult(true);
    }

    public Task<OAuthClientEntity?> GetApplicationAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return Task.FromResult<OAuthClientEntity?>(null);

        lock (_lock)
        {
            return Task.FromResult(_applications.TryGetValue(clientId, out var c) ? c : null);
        }
    }

    public Task<bool> VerifyClientAsync(string clientId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrEmpty(clientSecret))
            return Task.FromResult(false);

        lock (_lock)
        {
            if (!_applications.TryGetValue(clientId, out var c))
                return Task.FromResult(false);
            return Task.FromResult(c.ClientSecret == clientSecret);
        }
    }

    public Task<IReadOnlyList<OAuthClientEntity>> GetAllAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<OAuthClientEntity>>(_applications.Values.ToList());
        }
    }

    public Task<IReadOnlyList<OAuthClientEntity>> GetByOwnerAsync(string ownerActorId)
    {
        if (string.IsNullOrWhiteSpace(ownerActorId))
            return Task.FromResult<IReadOnlyList<OAuthClientEntity>>(Array.Empty<OAuthClientEntity>());

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<OAuthClientEntity>>(
                _applications.Values.Where(c => c.OwnerActorId == ownerActorId).ToList());
        }
    }
}
