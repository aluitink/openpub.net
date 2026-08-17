namespace ActivityPub.Core.Repositories;

/// <summary>
/// A registered third-party client application (Mastodon-style OAuth client
/// registration). <see cref="ClientId"/> is the public client id; the matching
/// <see cref="ClientSecret"/> is returned to the owner once at creation time.
/// </summary>
public class OAuthClientEntity
{
    public int Id { get; set; }

    /// <summary>Public client id (returned to the client).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Secret paired with the client id (shown once at creation).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Human-readable application name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Space-separated list of granted scopes (Mastodon: read write follow push).</summary>
    public string Scopes { get; set; } = "read write follow push";

    /// <summary>Redirect URIs the client is allowed to use (space or comma separated).</summary>
    public string RedirectUris { get; set; } = string.Empty;

    /// <summary>Optional application website.</summary>
    public string? Website { get; set; }

    /// <summary>First-party (native) clients get implicit approval; web apps must be approved.</summary>
    public bool FirstParty { get; set; }

    /// <summary>Actor (by ActivityPub id) that registered this client, or null for anonymous registration.</summary>
    public string? OwnerActorId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
