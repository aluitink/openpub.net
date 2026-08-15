using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DemoApp.Services.OAuth2;

public class AuthorizationCode
{
    public string Code { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? RedirectUri { get; set; }
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public HashSet<string> Scopes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }

    [JsonIgnore]
    public DateTime? UsedAt { get; set; }
}

public class AccessToken
{
    public string Token { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public HashSet<string> Scopes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}

public class RefreshToken
{
    public string Token { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public HashSet<string> Scopes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class OAuth2Constants
{
    public const string AuthorizationEndpoint = "/oauth2/authorize";
    public const string TokenEndpoint = "/oauth2/token";
    public const string UserInfoEndpoint = "/oauth2/userinfo";
    public const string RevokeEndpoint = "/oauth2/revoke";
    public const string IntrospectEndpoint = "/oauth2/introspect";

    public const string CodeChallengeMethodS256 = "S256";
    public const string CodeChallengeMethodPlain = "plain";

    public const int DefaultCodeExpiryMinutes = 10;
    public const int DefaultAccessTokenExpiryHours = 1;
    public const int DefaultRefreshTokenExpiryDays = 30;
}

public static class OAuth2Scopes
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Follow = "follow";
    public const string Manage = "manage";

    public static readonly HashSet<string> AllScopes = new()
    {
        Read, Write, Follow, Manage
    };

    public static readonly Dictionary<string, string> ScopeDescriptions = new()
    {
        { Read, "Read access to your activities and profile" },
        { Write, "Write access to post and create activities" },
        { Follow, "Access to manage followers and following" },
        { Manage, "Full access to account management" }
    };
}

public class TokenRequest
{
    public string GrantType { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? RedirectUri { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? CodeVerifier { get; set; }
    public string? RefreshToken { get; set; }
    public HashSet<string>? Scopes { get; set; }
}

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string? RefreshToken { get; set; }
    public HashSet<string>? Scopes { get; set; }
}

public class UserInfoResponse
{
    public string Sub { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Name { get; set; }
    public string? PreferredUsername { get; set; }
    public string? Profile { get; set; }
    public string? Picture { get; set; }
    public string? Website { get; set; }
    public string? Bio { get; set; }
    public HashSet<string>? Scopes { get; set; }
}

public class AuthorizationRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ResponseType { get; set; } = "code";
    public string? State { get; set; }
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public HashSet<string>? Scopes { get; set; }
}

public class AuthorizationResponse
{
    public string Code { get; set; } = string.Empty;
    public string? State { get; set; }
}

public class RevokeRequest
{
    public string Token { get; set; } = string.Empty;
    public string? ClientId { get; set; }
}

public class IntrospectRequest
{
    public string Token { get; set; } = string.Empty;
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

public class IntrospectResponse
{
    public bool Active { get; set; }
    public string? TokenType { get; set; }
    public string? ActorId { get; set; }
    public string? ClientId { get; set; }
    public HashSet<string>? Scopes { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? IssuedAt { get; set; }
}

public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public string? ActorId { get; set; }
    public string? ClientId { get; set; }
    public HashSet<string>? Scopes { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PKCECodeChallenge
{
    public string CodeChallenge { get; set; } = string.Empty;
    public string CodeChallengeMethod { get; set; } = OAuth2Constants.CodeChallengeMethodS256;
}
