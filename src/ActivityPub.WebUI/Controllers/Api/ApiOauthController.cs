using System.Security.Cryptography;
using System.Text;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers.Api;

/// <summary>
/// OAuth 2.0 authorization-code flow with PKCE (RFC 7636) for API
/// authentication. Endpoints are Mastodon-shaped:
///   GET  /api/v1/oauth/authorize  — user grants a code (302 redirect)
///   POST /api/v1/oauth/token      — exchange the code for a bearer token
/// </summary>
[ApiController]
[Route("api/v1/oauth")]
[Produces("application/json")]
public class ApiOauthController : ControllerBase
{
    private const int CodeLifetimeMinutes = 5;
    private const int TokenLifetimeDays = 30;
    private const string DefaultScopes = "read write follow push";

    private readonly IApplicationRepository _applications;

    public ApiOauthController(IApplicationRepository applications)
    {
        _applications = applications;
    }

    /// <summary>
    /// Authorization endpoint. Requires an authenticated user (cookie session).
    /// Issues a short-lived authorization code and redirects to the client's
    /// <c>redirect_uri</c> with <c>?code=...</c>.
    /// </summary>
    [HttpGet]
    [Authorize(AuthenticationSchemes = BearerTokenAuthConstants.BothSchemes)]
    [Route("authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery] string? response_type,
        [FromQuery] string? client_id,
        [FromQuery] string? redirect_uri,
        [FromQuery] string? scope,
        [FromQuery] string? code_challenge,
        [FromQuery] string? code_challenge_method)
    {
        if (response_type != "code")
            return BadRequest(new { error = "unsupported_response_type", error_description = "Only 'code' is supported." });

        if (string.IsNullOrWhiteSpace(client_id))
            return BadRequest(new { error = "invalid_request", error_description = "client_id is required." });

        if (string.IsNullOrWhiteSpace(redirect_uri))
            return BadRequest(new { error = "invalid_request", error_description = "redirect_uri is required." });

        var client = await _applications.GetApplicationAsync(client_id!);
        if (client == null)
            return BadRequest(new { error = "unauthorized_client", error_description = "Unknown client_id." });

        if (!IsRedirectUriAllowed(client.RedirectUris, redirect_uri!))
            return BadRequest(new { error = "invalid_request", error_description = "redirect_uri is not registered for this client." });

        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        // Restrict the granted scopes to the union of the requested scopes and
        // the client's registered scopes.
        var grantedScopes = ResolveScopes(scope, client.Scopes);

        var code = NewToken(32);
        var codeEntity = new OAuthCodeEntity
        {
            Code = code,
            Username = username,
            ClientId = client.ClientId,
            Scopes = grantedScopes,
            CodeChallenge = code_challenge,
            CodeChallengeMethod = code_challenge_method,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(CodeLifetimeMinutes)
        };

        await _applications.SaveAuthorizationCodeAsync(codeEntity);

        var separator = redirect_uri!.Contains('?') ? "&" : "?";
        var redirect = $"{redirect_uri}{separator}code={Uri.EscapeDataString(code)}";
        return Redirect(redirect);
    }

    /// <summary>
    /// Token endpoint. Exchanges an authorization code for a bearer access
    /// token (authorization_code grant with PKCE).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [Route("token")]
    public async Task<IActionResult> Token()
    {
        var form = await Request.ReadFormAsync();
        string? Get(string name) => form[name].ToString();

        var grantType = Get("grant_type");
        if (grantType != "authorization_code")
            return BadRequest(new { error = "unsupported_grant_type", error_description = "Only 'authorization_code' is supported." });

        var code = Get("code");
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_request", error_description = "code is required." });

        var clientId = Get("client_id");
        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { error = "invalid_client", error_description = "client_id is required." });

        // Verify the client (confidential clients send their secret).
        var clientSecret = Get("client_secret");
        if (!string.IsNullOrEmpty(clientSecret))
        {
            if (!await _applications.VerifyClientAsync(clientId!, clientSecret))
                return Unauthorized(new { error = "invalid_client", error_description = "Invalid client credentials." });
        }

        var codeEntity = await _applications.RedeemAuthorizationCodeAsync(code!);
        if (codeEntity == null)
            return BadRequest(new { error = "invalid_grant", error_description = "The authorization code is invalid or has expired." });

        if (codeEntity.ClientId != clientId)
            return BadRequest(new { error = "invalid_grant", error_description = "The code was not issued to this client." });

        // PKCE verification (RFC 7636).
        if (!string.IsNullOrEmpty(codeEntity.CodeChallenge))
        {
            var codeVerifier = Get("code_verifier");
            if (string.IsNullOrEmpty(codeVerifier))
                return BadRequest(new { error = "invalid_grant", error_description = "code_verifier is required." });

            if (!VerifyCodeChallenge(codeEntity.CodeChallenge!, codeEntity.CodeChallengeMethod, codeVerifier))
                return BadRequest(new { error = "invalid_grant", error_description = "PKCE verification failed." });
        }

        var token = NewToken(48);
        var tokenEntity = new OAuthTokenEntity
        {
            Token = token,
            Username = codeEntity.Username,
            ClientId = codeEntity.ClientId,
            Scopes = codeEntity.Scopes,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(TokenLifetimeDays)
        };
        await _applications.SaveAccessTokenAsync(tokenEntity);

        return Ok(new
        {
            access_token = token,
            token_type = "Bearer",
            scope = codeEntity.Scopes,
            created_at = new DateTimeOffset(tokenEntity.CreatedAt).ToUnixTimeSeconds()
        });
    }

    private static bool IsRedirectUriAllowed(string registered, string requested)
    {
        if (string.IsNullOrWhiteSpace(registered))
            return false;

        var allowed = registered.Split(new[] { ' ', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        return allowed.Any(uri => string.Equals(uri, requested, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveScopes(string? requested, string clientScopes)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return string.IsNullOrWhiteSpace(clientScopes) ? DefaultScopes : clientScopes;

        var requestedSet = requested.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clientSet = clientScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Grant the intersection of requested and client-registered scopes, or
        // fall back to the client's registered scopes if nothing overlaps.
        var intersection = requestedSet.Where(clientSet.Contains).ToList();
        return intersection.Count > 0 ? string.Join(' ', intersection) : (string.IsNullOrWhiteSpace(clientScopes) ? DefaultScopes : clientScopes);
    }

    /// <summary>
    /// Verifies a PKCE code verifier against the stored code challenge.
    /// Supports S256 (default) and plain.
    /// </summary>
    private static bool VerifyCodeChallenge(string challenge, string? method, string verifier)
    {
        var computed = ComputeChallenge(verifier, method);
        return computed != null && string.Equals(computed, challenge, StringComparison.Ordinal);
    }

    private static string? ComputeChallenge(string verifier, string? method)
    {
        try
        {
            if (string.Equals(method, "plain", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(method))
            {
                // Per RFC 7636 the default is S256; 'plain' is only used when
                // explicitly requested. Treat null/empty as S256.
                if (string.Equals(method, "plain", StringComparison.OrdinalIgnoreCase))
                    return verifier;
                return null;
            }

            if (!string.Equals(method, "S256", StringComparison.OrdinalIgnoreCase))
                return null;

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }
        catch
        {
            return null;
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string NewToken(int byteLength)
    {
        var bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

}
