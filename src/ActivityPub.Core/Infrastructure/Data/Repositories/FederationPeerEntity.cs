namespace ActivityPub.Core.Repositories;

/// <summary>
/// Tracks the reliability of a single remote ActivityPub server (identified by
/// its domain). The server uses these counters to automatically block peers
/// that fail to accept our outbound deliveries and to re-admit them once they
/// start succeeding again.
/// </summary>
public class FederationPeerEntity
{
    /// <summary>
    /// The peer's domain (e.g. "mastodon.social"). This is the unique key.
    /// </summary>
    public required string Domain { get; set; }

    /// <summary>
    /// Number of consecutive delivery failures without a success. Reset to zero
    /// on any successful delivery. Auto-blocking triggers when this reaches
    /// <see cref="Options.ActivityPubOptions.PeerHealthOptions.AutoBlockThreshold"/>.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Number of consecutive delivery successes without a failure. Reset to zero
    /// on any failed delivery. Auto-unblocking (re-admitting a blocked peer)
    /// triggers when this reaches
    /// <see cref="Options.ActivityPubOptions.PeerHealthOptions.AutoUnblockSuccessThreshold"/>.
    /// </summary>
    public int ConsecutiveSuccesses { get; set; }

    /// <summary>
    /// Total number of delivery attempts made to this domain, ever.
    /// </summary>
    public int TotalDeliveries { get; set; }

    /// <summary>
    /// Total number of failed delivery attempts, ever.
    /// </summary>
    public int TotalFailures { get; set; }

    /// <summary>
    /// When the most recent delivery attempt (success or failure) was made.
    /// </summary>
    public DateTime? LastDeliveryAttempt { get; set; }

    /// <summary>
    /// When the most recent successful delivery was made.
    /// </summary>
    public DateTime? LastSuccessfulDelivery { get; set; }

    /// <summary>
    /// When the most recent liveness probe was run and whether it was reachable.
    /// </summary>
    public DateTime? LastProbedAt { get; set; }
    public bool? LastProbeReachable { get; set; }

    /// <summary>
    /// Consecutive liveness-probe failures (unreachable probes). A peer that is
    /// unreachable for this many probes in a row is auto-blocked, independent
    /// of delivery outcomes.
    /// </summary>
    public int ConsecutiveProbeFailures { get; set; }

    /// <summary>
    /// Whether this peer is currently blocked from receiving our deliveries and
    /// from which we reject inbound activities.
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    /// When the peer was blocked.
    /// </summary>
    public DateTime? BlockedAt { get; set; }

    /// <summary>
    /// Human-readable reason the peer was blocked (e.g. "5 consecutive
    /// delivery failures", "manual block by admin").
    /// </summary>
    public string? BlockedReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
