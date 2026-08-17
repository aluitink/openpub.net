using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using ActivityPub.WebUI.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers.Api;

/// <summary>
/// Local REST API — webhook subscriptions for external integrations. A user
/// registers HTTP endpoints that receive a signed (HMAC-SHA256) notification
/// whenever a configured event type occurs on their account. Delivery is
/// durable and asynchronous: each event is queued and pumped by a background
/// service (see <c>WebhookDeliveryBackgroundService</c>).
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = BearerTokenAuthConstants.BothSchemes)]
[Route("api/v1")]
[Produces("application/json")]
public class ApiWebhooksController : ControllerBase
{
    private readonly IWebhookDeliveryService _webhooks;
    private readonly IActivityPubRepository _repository;

    public ApiWebhooksController(IWebhookDeliveryService webhooks, IActivityPubRepository repository)
    {
        _webhooks = webhooks;
        _repository = repository;
    }

    /// <summary>
    /// Resolves the current user's actor (and its ID), or null when the caller
    /// is not authenticated. Webhook subscriptions are keyed by actor ID.
    /// </summary>
    private async Task<ActivityPub.Core.Models.Actor?> ResolveActorAsync()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return null;
        return await _repository.GetUserActorAsync(username);
    }

    /// <summary>
    /// Registers (or updates) a webhook subscription for the current user.
    /// Returns the created subscription, including the secret key when one was
    /// supplied (so the client can verify the HMAC signatures).
    /// </summary>
    [HttpPost]
    [Route("webhooks")]
    public async Task<IActionResult> Create([FromBody] ApiWebhookCreateRequest? request)
    {
        var actor = await ResolveActorAsync();
        if (actor == null)
            return Unauthorized(new { error = "Authentication is required." });

        var endpointUrl = request?.EndpointUrl?.Trim();
        if (string.IsNullOrWhiteSpace(endpointUrl))
            return BadRequest(new { error = "The 'endpoint_url' field is required." });
        if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { error = "The 'endpoint_url' must be an absolute http(s) URL." });

        var httpMethod = string.IsNullOrWhiteSpace(request?.HttpMethod)
            ? "POST"
            : request!.HttpMethod.Trim().ToUpperInvariant();
        if (httpMethod != "POST" && httpMethod != "PUT")
            return BadRequest(new { error = "The 'http_method' must be POST or PUT." });

        var eventType = string.IsNullOrWhiteSpace(request?.EventType) ? "All" : request!.EventType.Trim();
        var enabled = request?.Enabled ?? true;
        var maxRetries = request?.MaxRetries ?? 3;
        var retryDelaySeconds = request?.RetryDelaySeconds ?? 60;

        var secretKey = string.IsNullOrWhiteSpace(request?.SecretKey)
            ? NewSecret()
            : request!.SecretKey.Trim();

        var saved = await _webhooks.ConfigureWebhookAsync(
            actor.Id ?? string.Empty, eventType, endpointUrl, httpMethod, enabled,
            secretKey, maxRetries, retryDelaySeconds, useExponentialBackoff: true);

        if (!saved)
            return Problem("Could not save the webhook subscription.", statusCode: StatusCodes.Status500InternalServerError);

        // Re-fetch to obtain the generated id + timestamp.
        var configs = (await _webhooks.GetWebhookConfigsAsync(actor.Id ?? string.Empty, eventType)).ToList();
        var config = configs.FirstOrDefault(c => c.EndpointUrl == endpointUrl) ?? configs.FirstOrDefault();
        if (config == null)
            return Problem("Could not save the webhook subscription.", statusCode: StatusCodes.Status500InternalServerError);

        return Ok(new ApiWebhook
        {
            Id = config.Id.ToString(),
            EndpointUrl = config.EndpointUrl,
            HttpMethod = config.HttpMethod,
            EventType = config.EventType,
            Enabled = config.Enabled,
            MaxRetries = config.MaxRetries,
            RetryDelaySeconds = config.RetryDelaySeconds,
            SecretKey = secretKey, // shown once, at creation
            CreatedAt = config.CreatedAt
        });
    }

    /// <summary>
    /// Lists the current user's webhook subscriptions. The secret key is never
    /// re-emitted.
    /// </summary>
    [HttpGet]
    [Route("webhooks")]
    public async Task<IActionResult> List()
    {
        var actor = await ResolveActorAsync();
        if (actor == null)
            return Unauthorized(new { error = "Authentication is required." });

        var configs = (await _webhooks.GetWebhookConfigsAsync(actor.Id ?? string.Empty)).ToList();

        return Ok(configs.Select(c => ToDto(c, includeSecret: false)));
    }

    /// <summary>
    /// Deletes a webhook subscription by id. The caller may only delete their
    /// own subscriptions.
    /// </summary>
    [HttpDelete]
    [Route("webhooks/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var actor = await ResolveActorAsync();
        if (actor == null)
            return Unauthorized(new { error = "Authentication is required." });

        var config = await _repository.GetWebhookConfigByIdAsync(id);
        if (config == null || config.ActorId != actor.Id)
            return NotFound(new { error = "Webhook not found." });

        var deleted = await _webhooks.DeleteWebhookConfigAsync(id);
        if (!deleted)
            return Problem("Could not delete the webhook.", statusCode: StatusCodes.Status500InternalServerError);

        return NoContent();
    }

    private static ApiWebhook ToDto(WebhookConfigEntity c, bool includeSecret) => new()
    {
        Id = c.Id.ToString(),
        EndpointUrl = c.EndpointUrl,
        HttpMethod = c.HttpMethod,
        EventType = c.EventType,
        Enabled = c.Enabled,
        MaxRetries = c.MaxRetries,
        RetryDelaySeconds = c.RetryDelaySeconds,
        SecretKey = includeSecret ? c.SecretKey : null,
        CreatedAt = c.CreatedAt
    };

    /// <summary>
    /// Generates a URL-safe random secret for HMAC signing.
    /// </summary>
    private static string NewSecret()
    {
        var bytes = new byte[24];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
