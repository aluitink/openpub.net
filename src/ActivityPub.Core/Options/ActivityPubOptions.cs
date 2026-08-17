namespace ActivityPub.Core.Options;

public class MRFOptions
{
    public List<string> ProhibitedWords { get; set; } = new();
    public List<string> BlockedDomains { get; set; } = new();
    public int? MaxContentLength { get; set; }
}

/// <summary>
/// Retry policy for outbound ActivityPub activity deliveries. When a delivery
/// attempt fails, the item is re-queued and becomes eligible for its next
/// attempt after a delay that grows (optionally) exponentially with each
/// consecutive failure, up to <see cref="MaxRetries"/> attempts.
/// </summary>
public class DeliveryRetryOptions
{
    /// <summary>
    /// Maximum number of delivery attempts before the item is marked
    /// <c>MaxRetriesExceeded</c> (the terminal, de-facto dead-letter state).
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Base delay, in seconds, before the first retry. Each subsequent retry
    /// delay is multiplied by 2 when <see cref="UseExponentialBackoff"/> is on,
    /// capped at <see cref="MaxRetryDelaySeconds"/>.
    /// </summary>
    public int BaseRetryDelaySeconds { get; set; } = 30;

    /// <summary>
    /// When true, each retry delay doubles from the previous one
    /// (base * 2^attempt). When false, every retry waits the flat
    /// <see cref="BaseRetryDelaySeconds"/>.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Upper bound, in seconds, for any single retry delay. Prevents a long
    /// streak of failures from pushing the next attempt hours out.
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 3600;
}

/// <summary>
/// Federation peer health tracking and auto-blocking policy. The server tracks
/// the reliability of each remote server it delivers to and automatically
/// blocks peers that are consistently unreliable (failing deliveries or
/// unreachable on liveness probes), and re-admits them once they start
/// succeeding again.
/// </summary>
public class PeerHealthOptions
{
    /// <summary>
    /// Master switch for peer health tracking and auto-blocking. When false,
    /// delivery outcomes and probes are still recorded but no auto-block /
    /// auto-unblock actions are taken.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive delivery failures (with no success in between)
    /// before a peer is automatically blocked.
    /// </summary>
    public int AutoBlockThreshold { get; set; } = 5;

    /// <summary>
    /// Number of consecutive delivery successes (with no failure in between)
    /// required to automatically unblock a previously-blocked peer.
    /// </summary>
    public int AutoUnblockSuccessThreshold { get; set; } = 3;

    /// <summary>
    /// Number of consecutive unreachable liveness probes before a peer is
    /// automatically blocked, independent of delivery outcomes.
    /// </summary>
    public int AutoBlockProbeFailureThreshold { get; set; } = 3;

    /// <summary>
    /// How often the background service probes known peers for liveness, in
    /// minutes.
    /// </summary>
    public int ProbeIntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Configuration options for ActivityPub
/// </summary>
public class ActivityPubOptions
{
    /// <summary>
    /// The domain/host for the ActivityPub server
    /// </summary>
    public string Domain { get; set; } = "localhost";

    /// <summary>
    /// Path prefix for user/actor endpoints
    /// </summary>
    public string UserPath { get; set; } = "/users";

    /// <summary>
    /// Path for inbox endpoint
    /// </summary>
    public string InboxPath { get; set; } = "/inbox";

    /// <summary>
    /// Path for outbox endpoint
    /// </summary>
    public string OutboxPath { get; set; } = "/outbox";

    /// <summary>
    /// Path for followers endpoint
    /// </summary>
    public string FollowersPath { get; set; } = "/followers";

    /// <summary>
    /// Path for following endpoint
    /// </summary>
    public string FollowingPath { get; set; } = "/following";

    /// <summary>
    /// Path for shared inbox endpoint
    /// </summary>
    public string SharedInboxPath { get; set; } = "/inbox";

    /// <summary>
    /// Maximum activity queue size
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// Enable HTTP signature verification
    /// </summary>
    public bool EnableSignatureVerification { get; set; } = true;

    /// <summary>
    /// When true (and <see cref="EnableSignatureVerification"/> is true), every
    /// inbound activity delivery must carry a valid HTTP signature; unsigned
    /// inbox POSTs are rejected with 401. When false, a present signature is
    /// still verified (and rejected if invalid) but unsigned requests are
    /// tolerated — the posture used for local development and testing.
    /// </summary>
    public bool RequireSignatures { get; set; } = false;

    /// <summary>
    /// Enable ActivityPub federation
    /// </summary>
    public bool EnableFederation { get; set; } = false;

    /// <summary>
    /// Message Rewrite Rules (MRF) options for content moderation
    /// </summary>
    public MRFOptions? MRFOptions { get; set; }

    /// <summary>
    /// Retry policy for outbound activity deliveries (see
    /// <see cref="DeliveryRetryOptions"/>).
    /// </summary>
    public DeliveryRetryOptions DeliveryRetry { get; set; } = new();

    /// <summary>
    /// Federation peer health tracking and auto-blocking policy (see
    /// <see cref="PeerHealthOptions"/>).
    /// </summary>
    public PeerHealthOptions PeerHealth { get; set; } = new();
}
