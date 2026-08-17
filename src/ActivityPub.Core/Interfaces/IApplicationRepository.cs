using ActivityPub.Core.Repositories;

namespace ActivityPub.Core.Interfaces;

/// <summary>
/// Persistence for registered third-party client applications (OAuth client
/// registration). Kept separate from <see cref="IActivityPubRepository"/> because
/// applications are a distinct concern from actors/activities.
/// </summary>
public interface IApplicationRepository
{
    /// <summary>
    /// Persists a newly registered client application.
    /// </summary>
    /// <param name="client">The client to save (ClientId/ClientSecret must be set).</param>
    /// <returns>True on success.</returns>
    Task<bool> SaveApplicationAsync(OAuthClientEntity client);

    /// <summary>
    /// Looks up a registered client by its public client id.
    /// </summary>
    /// <param name="clientId">The client id to find.</param>
    /// <returns>The client entity if found, otherwise null.</returns>
    Task<OAuthClientEntity?> GetApplicationAsync(string clientId);

    /// <summary>
    /// Verifies a client id + secret pair (used during token exchange).
    /// </summary>
    Task<bool> VerifyClientAsync(string clientId, string clientSecret);

    /// <summary>
    /// Returns every registered client application.
    /// </summary>
    Task<IReadOnlyList<OAuthClientEntity>> GetAllAsync();

    /// <summary>
    /// Returns the client applications registered by a given actor.
    /// </summary>
    Task<IReadOnlyList<OAuthClientEntity>> GetByOwnerAsync(string ownerActorId);
}
