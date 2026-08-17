namespace ActivityPub.Core.Repositories;

/// <summary>
/// A short-lived OAuth 2.0 authorization code (RFC 6749) paired with an
/// optional PKCE code challenge (RFC 7636). Keyed by <see cref="Username"/>
/// because the WebUI resolves API actors by username, not a numeric id.
/// </summary>
public class OAuthCodeEntity
{
    public int Id { get; set; }

    /// <summary>Opaque authorization code handed to the client.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Username of the actor that authorized the code.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Client id the code was issued to (must match on exchange).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Space-separated scopes granted for this code.</summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>Base64url(SHA-256(verbatim code verifier)) when PKCE is used.</summary>
    public string? CodeChallenge { get; set; }

    /// <summary>PKCE method: S256 (supported) or plain.</summary>
    public string? CodeChallengeMethod { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}

/// <summary>
/// A bearer access token for API authentication. Keyed by
/// <see cref="Username"/> so the Bearer handler can resolve the actor.
/// </summary>
public class OAuthTokenEntity
{
    public int Id { get; set; }

    /// <summary>The bearer token value (returned to the client).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Username the token was issued for.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Client id the token was issued to.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Space-separated scopes granted to this token.</summary>
    public string Scopes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
