using System.Security.Cryptography;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers.Api;

/// <summary>
/// Local REST API — third-party client application registration,
/// Mastodon-compatible (POST /api/v1/apps).
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class ApiAppsController : ControllerBase
{
    private const string DefaultScopes = "read write follow push";

    private readonly IApplicationRepository _applications;
    private readonly IActivityPubRepository _repository;

    public ApiAppsController(IApplicationRepository applications, IActivityPubRepository repository)
    {
        _applications = applications;
        _repository = repository;
    }

    /// <summary>
    /// Registers a new third-party client application. The client secret is
    /// returned once, at creation time only. If the caller is authenticated,
    /// the application is associated with that actor (used by GET /api/v1/apps).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [Route("apps")]
    public async Task<IActionResult> Register([FromBody] ApiAppRegistrationRequest? request)
    {
        var name = string.IsNullOrWhiteSpace(request?.Name) ? "Unnamed application" : request!.Name.Trim();

        // Mastodon accepts redirect_uris as a single string (space/newline/comma
        // separated) or a list of strings. Normalize to a single space-separated
        // string for storage.
        var redirectUris = NormalizeRedirectUris(request?.RedirectUris);

        var scopes = string.IsNullOrWhiteSpace(request?.Scopes) ? DefaultScopes : request!.Scopes.Trim();

        // Associate the application with the current user (if any) so the
        // authenticated list endpoint can scope results to them.
        string? ownerActorId = null;
        var username = User.Identity?.Name;
        if (!string.IsNullOrEmpty(username) && User.Identity!.IsAuthenticated)
        {
            var actor = await _repository.GetUserActorAsync(username);
            if (actor != null)
                ownerActorId = actor.Id;
        }

        var clientId = NewToken(24);
        var clientSecret = NewToken(24);

        var entity = new OAuthClientEntity
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Name = name,
            Scopes = scopes,
            RedirectUris = redirectUris,
            Website = request?.Website,
            FirstParty = false,
            OwnerActorId = ownerActorId,
            CreatedAt = DateTime.UtcNow
        };

        var saved = await _applications.SaveApplicationAsync(entity);
        if (!saved)
            return Problem("Could not register the application.", statusCode: StatusCodes.Status500InternalServerError);

        return Ok(new ApiApp
        {
            Id = entity.Id.ToString(),
            Name = name,
            Website = request?.Website,
            RedirectUri = FirstRedirectUri(redirectUris),
            ClientId = clientId,
            ClientSecret = clientSecret,
            VapidKey = string.Empty
        });
    }

    /// <summary>
    /// Lists the applications registered by the current user. Requires
    /// authentication. The client secret is never re-emitted.
    /// </summary>
    [HttpGet]
    [Authorize]
    [Route("apps")]
    public async Task<IActionResult> List()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
            return Unauthorized();

        var mine = (await _applications.GetByOwnerAsync(actor.Id)).ToList();

        return Ok(mine.Select(a => new ApiApp
        {
            Id = a.Id.ToString(),
            Name = a.Name,
            Website = a.Website,
            RedirectUri = FirstRedirectUri(a.RedirectUris),
            ClientId = a.ClientId,
            VapidKey = string.Empty
        }));
    }

    private static string NormalizeRedirectUris(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        return string.Join(' ',
            raw.Split(new[] { ' ', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Distinct());
    }

    private static string? FirstRedirectUri(string redirectUris)
        => string.IsNullOrEmpty(redirectUris) ? null : redirectUris.Split(' ')[0];

    /// <summary>
    /// Generates a URL-safe random token of the given byte length.
    /// </summary>
    private static string NewToken(int byteLength)
    {
        var bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
