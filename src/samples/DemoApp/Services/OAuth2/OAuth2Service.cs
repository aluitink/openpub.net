using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;

namespace DemoApp.Services.OAuth2;

public interface IOAuth2Service
{
    Task<AuthorizationResponse> CreateAuthorizationCodeAsync(AuthorizationRequest request, string actorId);
    Task<TokenResponse> CreateTokenAsync(TokenRequest request);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, string clientId);
    Task<bool> RevokeTokenAsync(string token, string? clientId);
    Task<IntrospectResponse> IntrospectTokenAsync(string token, string? clientId, string? clientSecret);
    Task<UserInfoResponse?> GetUserInfoAsync(string accessToken);
    Task<TokenValidationResult> ValidateAccessTokenAsync(string accessToken);
    Task<bool> ValidateCodeChallengeAsync(string codeVerifier, string codeChallenge, string? codeChallengeMethod);
    PKCECodeChallenge GeneratePKCECodeChallenge();
    Task<bool> ValidateClientAsync(string clientId, string? clientSecret);
}

public class OAuth2Service : IOAuth2Service
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ActivityPubDbContext _dbContext;
    private readonly IActorService _actorService;
    private readonly IKeyGenerationService _keyService;

    private const string CachePrefixAuthorizationCode = "oauth2_code_";
    private const string CachePrefixAccessToken = "oauth2_access_";
    private const string CachePrefixRefreshToken = "oauth2_refresh_";

    private const string DefaultClientId = "demoapp";
    private const string DefaultClientSecret = "demoapp-secret";

    public OAuth2Service(
        IMemoryCache cache,
        IConfiguration configuration,
        ActivityPubDbContext dbContext,
        IActorService actorService,
        IKeyGenerationService keyService)
    {
        _cache = cache;
        _configuration = configuration;
        _dbContext = dbContext;
        _actorService = actorService;
        _keyService = keyService;
    }

    public async Task<AuthorizationResponse> CreateAuthorizationCodeAsync(AuthorizationRequest request, string actorId)
    {
        var clientId = request.ClientId ?? DefaultClientId;

        if (!await ValidateClientAsync(clientId, null))
        {
            throw new InvalidOperationException("Invalid client");
        }

        if (string.IsNullOrEmpty(request.RedirectUri))
        {
            throw new InvalidOperationException("Redirect URI is required");
        }

        var scopes = request.Scopes ?? new HashSet<string>();

        if (!ValidateScopes(scopes))
        {
            throw new InvalidOperationException("Invalid scopes");
        }

        var code = GenerateSecureToken(32);
        var codeChallenge = request.CodeChallenge;
        var codeChallengeMethod = request.CodeChallengeMethod ?? OAuth2Constants.CodeChallengeMethodS256;

        var authorizationCode = new AuthorizationCode
        {
            Code = code,
            ActorId = actorId,
            ClientId = clientId,
            RedirectUri = request.RedirectUri,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            Scopes = scopes,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OAuth2Constants.DefaultCodeExpiryMinutes),
            IsUsed = false
        };

        _cache.Set($"{CachePrefixAuthorizationCode}{code}", authorizationCode, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(OAuth2Constants.DefaultCodeExpiryMinutes)
        });

        return new AuthorizationResponse
        {
            Code = code,
            State = request.State
        };
    }

    public async Task<TokenResponse> CreateTokenAsync(TokenRequest request)
    {
        if (request.GrantType == "authorization_code")
        {
            return await ExchangeAuthorizationCodeAsync(request);
        }
        else if (request.GrantType == "refresh_token")
        {
            return await RefreshTokenAsync(request.RefreshToken ?? string.Empty, request.ClientId ?? DefaultClientId);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported grant type: {request.GrantType}");
        }
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, string clientId)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new InvalidOperationException("Refresh token is required");
        }

        if (!await ValidateClientAsync(clientId, null))
        {
            throw new InvalidOperationException("Invalid client");
        }

        var cachedToken = _cache.Get<RefreshToken>($"{CachePrefixRefreshToken}{refreshToken}");

        if (cachedToken == null)
        {
            throw new InvalidOperationException("Invalid refresh token");
        }

        if (!cachedToken.IsActive)
        {
            throw new InvalidOperationException("Refresh token has been revoked");
        }

        if (DateTime.UtcNow > cachedToken.ExpiresAt)
        {
            throw new InvalidOperationException("Refresh token has expired");
        }

        var actor = await _actorService.GetActorByIdAsync(int.Parse(cachedToken.ActorId));
        if (actor == null)
        {
            throw new InvalidOperationException("Actor not found");
        }

        var newAccessToken = GenerateSecureToken(32);
        var newRefreshToken = GenerateSecureToken(32);

        var accessTokenEntity = new AccessToken
        {
            Token = newAccessToken,
            ActorId = cachedToken.ActorId,
            ClientId = clientId,
            Scopes = cachedToken.Scopes,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(OAuth2Constants.DefaultAccessTokenExpiryHours),
            RefreshToken = newRefreshToken
        };

        _cache.Set($"{CachePrefixAccessToken}{newAccessToken}", accessTokenEntity, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(OAuth2Constants.DefaultAccessTokenExpiryHours)
        });

        cachedToken.IsActive = false;
        _cache.Set($"{CachePrefixRefreshToken}{refreshToken}", cachedToken);

        var newRefreshTokenEntity = new RefreshToken
        {
            Token = newRefreshToken,
            ActorId = cachedToken.ActorId,
            ClientId = clientId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(OAuth2Constants.DefaultRefreshTokenExpiryDays),
            IsActive = true
        };

        _cache.Set($"{CachePrefixRefreshToken}{newRefreshToken}", newRefreshTokenEntity, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(OAuth2Constants.DefaultRefreshTokenExpiryDays)
        });

        return new TokenResponse
        {
            AccessToken = newAccessToken,
            TokenType = "Bearer",
            ExpiresIn = OAuth2Constants.DefaultAccessTokenExpiryHours * 3600,
            RefreshToken = newRefreshToken,
            Scopes = cachedToken.Scopes
        };
    }

    public async Task<bool> RevokeTokenAsync(string token, string? clientId)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var accessToken = _cache.Get<AccessToken>($"{CachePrefixAccessToken}{token}");
        if (accessToken != null)
        {
            _cache.Remove($"{CachePrefixAccessToken}{token}");
            return true;
        }

        var refreshToken = _cache.Get<RefreshToken>($"{CachePrefixRefreshToken}{token}");
        if (refreshToken != null)
        {
            refreshToken.IsActive = false;
            _cache.Set($"{CachePrefixRefreshToken}{token}", refreshToken);
            return true;
        }

        return false;
    }

    public async Task<IntrospectResponse> IntrospectTokenAsync(string token, string? clientId, string? clientSecret)
    {
        if (!await ValidateClientAsync(clientId ?? string.Empty, clientSecret))
        {
            return new IntrospectResponse { Active = false };
        }

        var accessToken = _cache.Get<AccessToken>($"{CachePrefixAccessToken}{token}");

        if (accessToken != null && DateTime.UtcNow <= accessToken.ExpiresAt)
        {
            return new IntrospectResponse
            {
                Active = true,
                TokenType = "Bearer",
                ActorId = accessToken.ActorId,
                ClientId = accessToken.ClientId,
                Scopes = accessToken.Scopes,
                ExpiresAt = accessToken.ExpiresAt,
                IssuedAt = accessToken.CreatedAt
            };
        }

        var refreshToken = _cache.Get<RefreshToken>($"{CachePrefixRefreshToken}{token}");

        if (refreshToken != null && refreshToken.IsActive && DateTime.UtcNow <= refreshToken.ExpiresAt)
        {
            return new IntrospectResponse
            {
                Active = true,
                TokenType = "RefreshToken",
                ActorId = refreshToken.ActorId,
                ClientId = refreshToken.ClientId,
                Scopes = refreshToken.Scopes,
                ExpiresAt = refreshToken.ExpiresAt,
                IssuedAt = refreshToken.CreatedAt
            };
        }

        return new IntrospectResponse { Active = false };
    }

    public async Task<UserInfoResponse?> GetUserInfoAsync(string accessToken)
    {
        var validation = await ValidateAccessTokenAsync(accessToken);

        if (!validation.IsValid)
        {
            return null;
        }

        var actorId = int.Parse(validation.ActorId!);
        var actor = await _actorService.GetActorByIdAsync(actorId);

        if (actor == null)
        {
            return null;
        }

        return new UserInfoResponse
        {
            Sub = actorId.ToString(),
            Username = actor.Username,
            Name = actor.Username,
            PreferredUsername = actor.Username,
            Profile = GetActorProfileUrl(actor.Username),
            Picture = GetActorAvatarUrl(actor.Username),
            Website = GetActorWebsiteUrl(actor.Username),
            Bio = "ActivityPub user",
            Scopes = validation.Scopes
        };
    }

    public async Task<TokenValidationResult> ValidateAccessTokenAsync(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Access token is required"
            };
        }

        var tokenData = _cache.Get<AccessToken>($"{CachePrefixAccessToken}{accessToken}");

        if (tokenData == null)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token not found"
            };
        }

        if (DateTime.UtcNow > tokenData.ExpiresAt)
        {
            _cache.Remove($"{CachePrefixAccessToken}{accessToken}");
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token has expired"
            };
        }

        return new TokenValidationResult
        {
            IsValid = true,
            ActorId = tokenData.ActorId,
            ClientId = tokenData.ClientId,
            Scopes = tokenData.Scopes
        };
    }

    public async Task<bool> ValidateCodeChallengeAsync(string codeVerifier, string codeChallenge, string? codeChallengeMethod)
    {
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(codeChallenge))
        {
            return true;
        }

        codeChallengeMethod ??= OAuth2Constants.CodeChallengeMethodS256;

        if (codeChallengeMethod == OAuth2Constants.CodeChallengeMethodS256)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(codeVerifier);
            var hash = sha256.ComputeHash(bytes);
            var computedChallenge = Base64UrlEncode(hash);

            return computedChallenge == codeChallenge;
        }
        else if (codeChallengeMethod == OAuth2Constants.CodeChallengeMethodPlain)
        {
            return codeVerifier == codeChallenge;
        }

        return false;
    }

    public PKCECodeChallenge GeneratePKCECodeChallenge()
    {
        var codeVerifier = GenerateSecureToken(64);
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(codeVerifier);
        var hash = sha256.ComputeHash(bytes);
        var codeChallenge = Base64UrlEncode(hash);

        return new PKCECodeChallenge
        {
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = OAuth2Constants.CodeChallengeMethodS256
        };
    }

    public async Task<bool> ValidateClientAsync(string clientId, string? clientSecret)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return false;
        }

        if (clientId == DefaultClientId)
        {
            if (string.IsNullOrEmpty(clientSecret))
            {
                return true;
            }
            return clientSecret == DefaultClientSecret;
        }

        return false;
    }

    private async Task<TokenResponse> ExchangeAuthorizationCodeAsync(TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
        {
            throw new InvalidOperationException("Authorization code is required");
        }

        if (string.IsNullOrEmpty(request.RedirectUri))
        {
            throw new InvalidOperationException("Redirect URI is required");
        }

        if (!await ValidateClientAsync(request.ClientId ?? DefaultClientId, request.ClientSecret))
        {
            throw new InvalidOperationException("Invalid client credentials");
        }

        var authorizationCode = _cache.Get<AuthorizationCode>($"{CachePrefixAuthorizationCode}{request.Code}");

        if (authorizationCode == null)
        {
            throw new InvalidOperationException("Invalid or expired authorization code");
        }

        if (authorizationCode.IsUsed)
        {
            throw new InvalidOperationException("Authorization code has already been used");
        }

        if (DateTime.UtcNow > authorizationCode.ExpiresAt)
        {
            _cache.Remove($"{CachePrefixAuthorizationCode}{request.Code}");
            throw new InvalidOperationException("Authorization code has expired");
        }

        if (!await ValidateCodeChallengeAsync(
            request.CodeVerifier ?? string.Empty,
            authorizationCode.CodeChallenge ?? string.Empty,
            authorizationCode.CodeChallengeMethod))
        {
            throw new InvalidOperationException("Invalid code verifier");
        }

        if (authorizationCode.RedirectUri != request.RedirectUri)
        {
            throw new InvalidOperationException("Redirect URI mismatch");
        }

        authorizationCode.IsUsed = true;
        _cache.Set($"{CachePrefixAuthorizationCode}{request.Code}", authorizationCode, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });

        var clientId = authorizationCode.ClientId;
        var actorId = authorizationCode.ActorId;
        var scopes = authorizationCode.Scopes;

        var actor = await _actorService.GetActorByIdAsync(int.Parse(actorId));
        if (actor == null)
        {
            throw new InvalidOperationException("Actor not found");
        }

        var newAccessToken = GenerateSecureToken(32);
        var newRefreshToken = GenerateSecureToken(32);

        var accessTokenEntity = new AccessToken
        {
            Token = newAccessToken,
            ActorId = actorId,
            ClientId = clientId,
            Scopes = scopes,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(OAuth2Constants.DefaultAccessTokenExpiryHours),
            RefreshToken = newRefreshToken
        };

        _cache.Set($"{CachePrefixAccessToken}{newAccessToken}", accessTokenEntity, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(OAuth2Constants.DefaultAccessTokenExpiryHours)
        });

        var refreshTokenEntity = new RefreshToken
        {
            Token = newRefreshToken,
            ActorId = actorId,
            ClientId = clientId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(OAuth2Constants.DefaultRefreshTokenExpiryDays),
            IsActive = true
        };

        _cache.Set($"{CachePrefixRefreshToken}{newRefreshToken}", refreshTokenEntity, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(OAuth2Constants.DefaultRefreshTokenExpiryDays)
        });

        return new TokenResponse
        {
            AccessToken = newAccessToken,
            TokenType = "Bearer",
            ExpiresIn = OAuth2Constants.DefaultAccessTokenExpiryHours * 3600,
            RefreshToken = newRefreshToken,
            Scopes = scopes
        };
    }

    private bool ValidateScopes(HashSet<string> scopes)
    {
        foreach (var scope in scopes)
        {
            if (!OAuth2Scopes.AllScopes.Contains(scope))
            {
                return false;
            }
        }
        return true;
    }

    private string GenerateSecureToken(int length)
    {
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("/", "_").Replace("+", "-").TrimEnd('=');
    }

    private string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input).Replace("/", "_").Replace("+", "-").TrimEnd('=');
    }

    private string GetActorProfileUrl(string username)
    {
        var domain = _configuration["ActivityPub:Domain"] ?? "localhost";
        var userPath = _configuration["ActivityPub:UserPath"] ?? "/users";
        return $"http://{domain}{userPath}/{username}";
    }

    private string GetActorAvatarUrl(string username)
    {
        var domain = _configuration["ActivityPub:Domain"] ?? "localhost";
        var userPath = _configuration["ActivityPub:UserPath"] ?? "/users";
        return $"http://{domain}{userPath}/{username}/avatar";
    }

    private string GetActorWebsiteUrl(string username)
    {
        var domain = _configuration["ActivityPub:Domain"] ?? "localhost";
        var userPath = _configuration["ActivityPub:UserPath"] ?? "/users";
        return $"http://{domain}{userPath}/{username}";
    }
}
