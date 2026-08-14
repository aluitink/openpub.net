using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Caching;

/// <summary>
/// Service for handling cache invalidation when data is updated
/// </summary>
public class CacheInvalidationService
{
    private readonly IFederationCache _cache;
    private readonly ILogger<CacheInvalidationService> _logger;

    /// <summary>
    /// Creates a new CacheInvalidationService instance
    /// </summary>
    /// <param name="cache">The federation cache to invalidate</param>
    /// <param name="logger">Logger for tracking invalidation operations</param>
    public CacheInvalidationService(
        IFederationCache cache,
        ILogger<CacheInvalidationService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invalidates actor cache when an actor is updated
    /// </summary>
    /// <param name="actor">The updated actor</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InvalidateActorCacheAsync(Actor actor, CancellationToken cancellationToken = default)
    {
        if (actor == null)
            return;

        var domain = ExtractDomain(actor.Id);
        
        _logger.LogInformation("Invalidating actor cache for domain: {Domain}", domain);
        
        await _cache.InvalidateActorsByDomainAsync(domain);
        
        if (!string.IsNullOrEmpty(actor.PreferredUsername))
        {
            await _cache.RemoveActorAsync($"actor:{actor.PreferredUsername}");
        }
    }

    /// <summary>
    /// Invalidates activity cache when an activity is updated or deleted
    /// </summary>
    /// <param name="activity">The updated or deleted activity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InvalidateActivityCacheAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        if (activity == null)
            return;

        _logger.LogInformation("Invalidating activity cache for activity: {ActivityId}", activity.Id);
        
        await _cache.RemoveActivityAsync(activity.Id);
        
        var actorId = ExtractActorId(activity.Actor);
        if (!string.IsNullOrEmpty(actorId))
        {
            await _cache.InvalidateActivitiesByActorAsync(actorId);
        }
    }

    /// <summary>
    /// Invalidates WebFinger cache when an actor's WebFinger data changes
    /// </summary>
    /// <param name="actor">The actor with updated WebFinger data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InvalidateWebFingerCacheAsync(Actor actor, CancellationToken cancellationToken = default)
    {
        if (actor == null)
            return;

        var domain = ExtractDomain(actor.Id);
        
        _logger.LogInformation("Invalidating WebFinger cache for domain: {Domain}", domain);
        
        await _cache.InvalidateWebFingerByDomainAsync(domain);
    }

    /// <summary>
    /// Invalidates inbox response cache when inbox state changes
    /// </summary>
    /// <param name="actorId">The actor whose inbox changed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InvalidateInboxCacheAsync(string actorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(actorId))
            return;

        _logger.LogInformation("Invalidating inbox cache for actor: {ActorId}", actorId);
        
        await _cache.InvalidateInboxResponsesByActorAsync(actorId);
    }

    /// <summary>
    /// Invalidates all caches related to a domain
    /// </summary>
    /// <param name="domain">The domain to invalidate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InvalidateAllForDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(domain))
            return;

        _logger.LogInformation("Invalidating all caches for domain: {Domain}", domain);
        
        await _cache.InvalidateActorsByDomainAsync(domain);
        await _cache.InvalidateWebFingerByDomainAsync(domain);
    }

    /// <summary>
    /// Invalidates all caches related to an actor
    /// </summary>
    /// <param name="actorId">The actor ID to invalidate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InvalidateAllForActorAsync(string actorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(actorId))
            return;

        _logger.LogInformation("Invalidating all caches for actor: {ActorId}", actorId);
        
        var domain = ExtractDomain(actorId);
        
        await _cache.InvalidateActorsByDomainAsync(domain);
        await _cache.InvalidateActivitiesByActorAsync(actorId);
        await _cache.InvalidateInboxResponsesByActorAsync(actorId);
        await _cache.InvalidateWebFingerByDomainAsync(domain);
    }

    /// <summary>
    /// Extracts domain from a URL
    /// </summary>
    /// <param name="url">The URL to extract domain from</param>
    /// <returns>The domain, or null if extraction fails</returns>
    private string? ExtractDomain(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts actor ID from actor object
    /// </summary>
    /// <param name="actor">The actor object</param>
    /// <returns>The actor ID, or null if extraction fails</returns>
    private string? ExtractActorId(object? actor)
    {
        return actor switch
        {
            string id => id,
            Actor a => a.Id,
            _ => null
        };
    }
}
