namespace ActivityPub.Core.Options;

public class MRFOptions
{
    public List<string> ProhibitedWords { get; set; } = new();
    public List<string> BlockedDomains { get; set; } = new();
    public int? MaxContentLength { get; set; }
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
}
