using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace DemoApp.Services;

public class TokenInfo
{
    public string Token { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public string? ActorId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TokenService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, TokenInfo> _tokens;
    private const int DefaultTokenExpiryHours = 24;

    public TokenService(IMemoryCache cache)
    {
        _cache = cache;
        _tokens = new ConcurrentDictionary<string, TokenInfo>();
        _cache.Set("token_service_initialized", true);
    }

    public string GenerateToken(string actorId, string? description = null, int expiryHours = 24)
    {
        var token = GenerateSecureToken();
        var tokenInfo = new TokenInfo
        {
            Token = token,
            ActorId = actorId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(expiryHours),
            IsActive = true,
            Description = description
        };

        _tokens.TryAdd(token, tokenInfo);
        _cache.Set($"token_{token}", tokenInfo, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(expiryHours)
        });

        return token;
    }

    public TokenValidationResult ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token is required"
            };
        }

        if (!_tokens.TryGetValue(token, out var tokenInfo))
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid token"
            };
        }

        if (!tokenInfo.IsActive)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token is disabled"
            };
        }

        if (DateTime.UtcNow > tokenInfo.ExpiresAt)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token has expired"
            };
        }

        return new TokenValidationResult
        {
            IsValid = true,
            ActorId = tokenInfo.ActorId
        };
    }

    public bool RevokeToken(string token)
    {
        if (_tokens.TryGetValue(token, out var tokenInfo))
        {
            tokenInfo.IsActive = false;
            _tokens[token] = tokenInfo;
            _cache.Remove($"token_{token}");
            return true;
        }

        return false;
    }

    public TokenInfo? GetTokenInfo(string token)
    {
        return _tokens.TryGetValue(token, out var info) ? info : null;
    }

    public List<TokenInfo> GetAllTokens()
    {
        return _tokens.Values.ToList();
    }

    private string GenerateSecureToken()
    {
        var tokenBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);
        return Convert.ToBase64String(tokenBytes);
    }
}
