using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActivityPub.Core.Services;

/// <summary>
/// Tracks the reliability of remote ActivityPub peers (servers, identified by
/// domain) and automatically blocks peers that are consistently unreliable.
///
/// Delivery outcomes are recorded via <see cref="RecordDeliveryOutcomeAsync"/>
/// (called from the outbound delivery path). A peer that accumulates
/// <see cref="PeerHealthOptions.AutoBlockThreshold"/> consecutive failures is
/// auto-blocked; a blocked peer that then accumulates
/// <see cref="PeerHealthOptions.AutoUnblockSuccessThreshold"/> consecutive
/// successes is auto-re-admitted.
///
/// Liveness probes are recorded via <see cref="RecordProbeOutcomeAsync"/>
/// (called from the periodic background service). A peer that is unreachable
/// for <see cref="PeerHealthOptions.AutoBlockProbeFailureThreshold"/> probes in
/// a row is auto-blocked regardless of delivery outcomes.
///
/// Outbound delivery consults <see cref="IsDomainBlockedAsync"/> before sending,
/// and the inbound path consults it to reject activities from blocked peers.
/// </summary>
public interface IPeerHealthService
{
    /// <summary>
    /// Records the outcome of an outbound delivery attempt to a domain and
    /// applies the auto-block / auto-unblock policy. Safe to call for any
    /// outcome; creates the peer record on first sight.
    /// </summary>
    /// <returns>Whether the peer is blocked as a result of this recording.</returns>
    Task<bool> RecordDeliveryOutcomeAsync(string domain, bool success, string? failureReason = null);

    /// <summary>
    /// Records the outcome of a liveness probe to a domain and applies the
    /// auto-block policy for sustained unreachability.
    /// </summary>
    Task<bool> RecordProbeOutcomeAsync(string domain, bool reachable);

    /// <summary>
    /// Returns whether a domain is currently blocked. Always returns false when
    /// <see cref="PeerHealthOptions.Enabled"/> is false or the domain is not
    /// known.
    /// </summary>
    Task<bool> IsDomainBlockedAsync(string domain);

    /// <summary>
    /// Returns a snapshot of the currently-blocked domain names. Intended for
    /// the inbound rejection path, which can cache this to avoid a DB hit per
    /// activity.
    /// </summary>
    Task<ICollection<string>> GetBlockedDomainsAsync();

    /// <summary>
    /// Manually blocks a domain (e.g. from an admin action).
    /// </summary>
    Task BlockDomainAsync(string domain, string reason = "manual block");

    /// <summary>
    /// Manually unblocks a domain and resets its failure streak.
    /// </summary>
    Task UnblockDomainAsync(string domain);

    /// <summary>
    /// Gets all tracked peers, optionally only blocked ones.
    /// </summary>
    Task<ICollection<FederationPeerEntity>> GetPeersAsync(bool onlyBlocked = false);
}

/// <summary>
/// Default implementation of <see cref="IPeerHealthService"/> backed by
/// <see cref="IActivityPubRepository"/>.
/// </summary>
public class PeerHealthService : IPeerHealthService
{
    private readonly IActivityPubRepository _repository;
    private readonly IOptions<ActivityPubOptions> _options;
    private readonly ILogger<PeerHealthService> _logger;

    public PeerHealthService(
        IActivityPubRepository repository,
        IOptions<ActivityPubOptions> options,
        ILogger<PeerHealthService> logger)
    {
        _repository = repository;
        _options = options;
        _logger = logger;
    }

    private PeerHealthOptions Options => _options.Value.PeerHealth;

    public async Task<bool> RecordDeliveryOutcomeAsync(string domain, bool success, string? failureReason = null)
    {
        if (string.IsNullOrEmpty(domain)) return false;

        var peer = await _repository.GetFederationPeerAsync(domain) ?? new FederationPeerEntity
        {
            Domain = domain,
            CreatedAt = DateTime.UtcNow
        };

        var now = DateTime.UtcNow;
        peer.TotalDeliveries++;
        peer.LastDeliveryAttempt = now;

        if (success)
        {
            peer.ConsecutiveFailures = 0;
            peer.ConsecutiveSuccesses++;
            peer.LastSuccessfulDelivery = now;

            // A blocked peer that has now succeeded enough times in a row is
            // re-admitted.
            if (peer.IsBlocked && Options.Enabled &&
                peer.ConsecutiveSuccesses >= Math.Max(1, Options.AutoUnblockSuccessThreshold))
            {
                peer.IsBlocked = false;
                peer.BlockedAt = null;
                peer.BlockedReason = null;
                peer.ConsecutiveSuccesses = 0;
                _logger.LogInformation("Auto-unblocked peer {Domain} after {N} consecutive successful deliveries",
                    domain, Options.AutoUnblockSuccessThreshold);
            }
        }
        else
        {
            peer.ConsecutiveSuccesses = 0;
            peer.ConsecutiveFailures++;
            peer.TotalFailures++;

            // A peer that has now failed enough times in a row is blocked.
            if (!peer.IsBlocked && Options.Enabled &&
                peer.ConsecutiveFailures >= Math.Max(1, Options.AutoBlockThreshold))
            {
                peer.IsBlocked = true;
                peer.BlockedAt = now;
                peer.BlockedReason = failureReason is null
                    ? $"{peer.ConsecutiveFailures} consecutive delivery failures"
                    : $"{peer.ConsecutiveFailures} consecutive delivery failures ({failureReason})";
                _logger.LogWarning("Auto-blocked peer {Domain} after {N} consecutive delivery failures",
                    domain, peer.ConsecutiveFailures);
            }
        }

        peer.UpdatedAt = now;
        await _repository.SaveFederationPeerAsync(peer);
        return peer.IsBlocked;
    }

    public async Task<bool> RecordProbeOutcomeAsync(string domain, bool reachable)
    {
        if (string.IsNullOrEmpty(domain)) return false;

        var peer = await _repository.GetFederationPeerAsync(domain) ?? new FederationPeerEntity
        {
            Domain = domain,
            CreatedAt = DateTime.UtcNow
        };

        var now = DateTime.UtcNow;
        peer.LastProbedAt = now;
        peer.LastProbeReachable = reachable;

        if (reachable)
        {
            peer.ConsecutiveProbeFailures = 0;
        }
        else
        {
            peer.ConsecutiveProbeFailures++;
            if (!peer.IsBlocked && Options.Enabled &&
                peer.ConsecutiveProbeFailures >= Math.Max(1, Options.AutoBlockProbeFailureThreshold))
            {
                peer.IsBlocked = true;
                peer.BlockedAt = now;
                peer.BlockedReason = $"{peer.ConsecutiveProbeFailures} consecutive unreachable liveness probes";
                _logger.LogWarning("Auto-blocked peer {Domain} after {N} consecutive unreachable liveness probes",
                    domain, peer.ConsecutiveProbeFailures);
            }
        }

        peer.UpdatedAt = now;
        await _repository.SaveFederationPeerAsync(peer);
        return peer.IsBlocked;
    }

    public async Task<bool> IsDomainBlockedAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return false;
        var peer = await _repository.GetFederationPeerAsync(domain);
        return peer?.IsBlocked == true;
    }

    public Task<ICollection<string>> GetBlockedDomainsAsync()
    {
        return _repository.GetBlockedDomainNamesAsync();
    }

    public async Task BlockDomainAsync(string domain, string reason = "manual block")
    {
        if (string.IsNullOrEmpty(domain)) return;

        var peer = await _repository.GetFederationPeerAsync(domain) ?? new FederationPeerEntity
        {
            Domain = domain,
            CreatedAt = DateTime.UtcNow
        };

        var now = DateTime.UtcNow;
        peer.IsBlocked = true;
        peer.BlockedAt = now;
        peer.BlockedReason = reason;
        peer.UpdatedAt = now;
        await _repository.SaveFederationPeerAsync(peer);
    }

    public async Task UnblockDomainAsync(string domain)
    {
        if (string.IsNullOrEmpty(domain)) return;

        var peer = await _repository.GetFederationPeerAsync(domain);
        if (peer == null || !peer.IsBlocked) return;

        var now = DateTime.UtcNow;
        peer.IsBlocked = false;
        peer.BlockedAt = null;
        peer.BlockedReason = null;
        peer.ConsecutiveFailures = 0;
        peer.ConsecutiveSuccesses = 0;
        peer.ConsecutiveProbeFailures = 0;
        peer.UpdatedAt = now;
        await _repository.SaveFederationPeerAsync(peer);
    }

    public Task<ICollection<FederationPeerEntity>> GetPeersAsync(bool onlyBlocked = false)
    {
        return _repository.GetFederationPeersAsync(onlyBlocked);
    }
}
