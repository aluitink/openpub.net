using System.Security.Claims;
using System.Text.Encodings.Web;
using ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActivityPub.WebUI.Auth;

public static class BearerTokenAuthConstants
{
    public const string SchemeName = "BearerToken";
    public const string ScopeClaim = "api.scope";
    public const string ClientIdClaim = "api.client_id";

    /// <summary>
    /// Comma-separated scheme list accepting the cookie (Identity.Application)
    /// or a Bearer token. Usable as a constant in [Authorize] attributes.
    /// </summary>
    public const string BothSchemes = "Identity.Application,BearerToken";
}

/// <summary>
/// Authentication handler for API Bearer tokens issued by
/// <c>POST /api/v1/oauth/token</c>. Validates the token against
/// <see cref="IApplicationRepository"/> and maps the owning user onto the
/// <see cref="ClaimsPrincipal"/> so <c>ControllerBase.User.Identity.Name</c>
/// resolves to the username, matching cookie-auth behavior.
/// </summary>
public class BearerTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IServiceProvider _services;

    public BearerTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceProvider services)
        : base(options, logger, encoder)
    {
        _services = services;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var header = authValues.ToString();
        const string prefix = "Bearer ";
        if (string.IsNullOrEmpty(header) ||
            !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = header[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return Task.FromResult(AuthenticateResult.NoResult());

        var repo = _services.GetRequiredService<IApplicationRepository>();
        return CompleteAuthenticateAsync(repo, token);
    }

    private async Task<AuthenticateResult> CompleteAuthenticateAsync(
        IApplicationRepository repo, string token)
    {
        var entity = await repo.GetAccessTokenAsync(token);
        if (entity == null)
            return AuthenticateResult.NoResult();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, entity.Username),
            new(BearerTokenAuthConstants.ScopeClaim, entity.Scopes),
            new(BearerTokenAuthConstants.ClientIdClaim, entity.ClientId),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name,
            nameType: ClaimTypes.Name, roleType: ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = entity.ExpiresAt,
        };
        properties.Items["client_id"] = entity.ClientId;

        var ticket = new AuthenticationTicket(principal, properties, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
