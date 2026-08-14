using System.Text.Json.Serialization;

namespace ActivityPub.Core.Repositories;

public class OAuth2AuthorizationCodeEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int ActorId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? RedirectUri { get; set; }
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public string Scopes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    
    [JsonIgnore]
    public ActorEntity Actor { get; set; } = null!;
}

public class OAuth2AccessTokenEntity
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public int ActorId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    
    [JsonIgnore]
    public ActorEntity Actor { get; set; } = null!;
}

public class OAuth2RefreshTokenEntity
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public int ActorId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    [JsonIgnore]
    public ActorEntity Actor { get; set; } = null!;
}
