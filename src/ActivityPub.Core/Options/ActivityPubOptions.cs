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
/// Error handling and dead-letter policy for inbound ActivityPub activities
/// (activities delivered to our inbox by remote servers). When processing an
/// inbound activity fails, the item is retried with exponential backoff up to
/// <see cref="MaxAttempts"/> times; after that it is moved to the inbound
/// dead-letter queue (see <see cref="InboxDeadLetterEntity"/>) where it is kept
/// for inspection until its age exceeds <see cref="DlqRetentionDays"/>.
/// </summary>
public class InboxProcessingOptions
{
    /// <summary>
    /// Master switch for inbound retry/dead-lettering. When false, a failed
    /// inbound activity is rejected immediately with no retry and no DLQ row
    /// (the pre-existing behavior).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of processing attempts (the initial attempt plus retries)
    /// before the item is moved to the dead-letter queue.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay, in seconds, before the first retry. Each subsequent retry
    /// delay is multiplied by 2 when <see cref="UseExponentialBackoff"/> is on,
    /// capped at <see cref="MaxRetryDelaySeconds"/>.
    /// </summary>
    public int BaseRetryDelaySeconds { get; set; } = 30;

    /// <summary>
    /// When true, each retry delay doubles from the previous one
    /// (base * 2^(attempt - 1)). When false, every retry waits the flat
    /// <see cref="BaseRetryDelaySeconds"/>.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Upper bound, in seconds, for any single retry delay.
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 3600;

    /// <summary>
    /// How long, in days, dead-lettered items are kept before the background
    /// service purges them. A value of 0 disables purging.
    /// </summary>
    public int DlqRetentionDays { get; set; } = 7;
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
/// The cache backend to use for federation caching.
/// </summary>
public enum CacheProvider
{
    /// <summary>
    /// In-memory cache (per-process). Default for development and single-instance deployments.
    /// </summary>
    Memory = 0,

    /// <summary>
    /// Redis-backed distributed cache. Use for multi-instance deployments where all instances
    /// must share the same cached state.
    /// </summary>
    Redis = 1
}

/// <summary>
/// Configuration for the federation cache backend. When <see cref="Provider"/> is
/// <see cref="CacheProvider.Redis"/>, the cache is stored in Redis and shared across
/// all application instances; when <see cref="CacheProvider.Memory"/> (the default),
/// each process keeps its own in-memory cache.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// The cache backend to use. Defaults to <see cref="CacheProvider.Memory"/>.
    /// </summary>
    public CacheProvider Provider { get; set; } = CacheProvider.Memory;

    /// <summary>
    /// Redis connection string (e.g. "localhost:6379" or "redis.example.com:6379,abortConnect=false").
    /// Only used when <see cref="Provider"/> is <see cref="CacheProvider.Redis"/>.
    /// </summary>
    public string RedisConnection { get; set; } = "localhost:6379";

    /// <summary>
    /// Optional prefix applied to all cache keys stored in Redis. Useful when multiple
    /// applications share a single Redis instance. Defaults to "activitypub:".
    /// </summary>
    public string CachePrefix { get; set; } = "activitypub:";
}

/// <summary>
/// Configuration for real-time (SignalR) scaling across multiple application
/// instances. When <see cref="Enabled"/> is true, a Redis backplane is used so
/// that hub messages broadcast on one instance are delivered to clients connected
/// to any other instance in the pool, and per-connection rate limiting is shared
/// across instances. When <see cref="Enabled"/> is false (the default), SignalR
/// runs in single-process mode with in-memory rate limiting.
/// </summary>
public class RealtimeOptions
{
    /// <summary>
    /// Whether to enable the Redis-based SignalR backplane (scale-out). Defaults to
    /// <see langword="false"/> for single-instance deployments.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Redis connection string for the SignalR backplane (e.g. "localhost:6379" or
    /// "redis.example.com:6379,abortConnect=false"). Defaults to the same value as the
    /// federation cache so a single Redis instance can serve both. Only used when
    /// <see cref="Enabled"/> is true.
    /// </summary>
    public string RedisConnection { get; set; } = "localhost:6379";

    /// <summary>
    /// Prefix for the Redis pub/sub channel used by the SignalR backplane. Useful when
    /// multiple SignalR applications share a single Redis instance. Defaults to
    /// "activitypub:signalr:".
    /// </summary>
    public string ChannelPrefix { get; set; } = "activitypub:signalr:";

    /// <summary>
    /// Maximum number of messages per connection per sliding window before the
    /// connection is rate-limited. Defaults to 50.
    /// </summary>
    public int MaxMessagesPerWindow { get; set; } = 50;

    /// <summary>
    /// Length of the rate-limit sliding window. Defaults to 1 minute.
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// The relational database provider used for persistence.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// SQLite (default). Lightweight, file-based, zero-configuration. Suitable for
    /// development and single-node deployments.
    /// </summary>
    Sqlite = 0,

    /// <summary>
    /// PostgreSQL. Use for production, multi-instance, or high-throughput
    /// deployments. Requires a running PostgreSQL server and the connection
    /// strings in <see cref="DatabaseOptions"/>.
    /// </summary>
    Postgresql = 1
}

/// <summary>
/// Configuration for the relational database provider and connection strings.
/// The application uses two databases: one for ASP.NET Core Identity (users and
/// roles) and one for ActivityPub federation data. When <see cref="Provider"/>
/// is <see cref="DatabaseProvider.Postgresql"/>, the PostgreSQL connection
/// strings are used; otherwise the SQLite file paths (derived from
/// <see cref="DataDirectory"/>) are used.
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// The relational database provider. Defaults to <see cref="DatabaseProvider.Sqlite"/>.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>
    /// Connection string for the Identity database. For SQLite this is a file path
    /// (e.g. "Data Source=fediblog.db"); for PostgreSQL a full connection string
    /// (e.g. "Host=localhost;Database=fediblog_identity;Username=ap;Password=...").
    /// When null, a SQLite file in <see cref="DataDirectory"/> is used.
    /// </summary>
    public string? IdentityConnection { get; set; }

    /// <summary>
    /// Connection string for the ActivityPub federation database. For SQLite this is
    /// a file path (e.g. "Data Source=fediblog_ap.db"); for PostgreSQL a full
    /// connection string (e.g. "Host=localhost;Database=fediblog_ap;Username=ap;Password=...").
    /// When null, a SQLite file in <see cref="DataDirectory"/> is used.
    /// </summary>
    public string? FederationConnection { get; set; }

    /// <summary>
    /// Directory in which SQLite database files are stored when the provider is
    /// SQLite and no explicit connection string is given. Defaults to the current
    /// directory.
    /// </summary>
    public string DataDirectory { get; set; } = ".";

    /// <summary>
    /// File name of the Identity SQLite database. Defaults to "fediblog.db".
    /// </summary>
    public string IdentityDatabaseFile { get; set; } = "fediblog.db";

    /// <summary>
    /// File name of the federation SQLite database. Defaults to "fediblog_ap.db".
    /// </summary>
    public string FederationDatabaseFile { get; set; } = "fediblog_ap.db";

    /// <summary>
    /// Resolves the effective connection string for the Identity database, honoring
    /// the configured provider and falling back to a SQLite file in
    /// <see cref="DataDirectory"/>.
    /// </summary>
    public string GetIdentityConnectionString() =>
        Provider == DatabaseProvider.Postgresql
            ? IdentityConnection ?? "Host=localhost;Database=fediblog_identity;Username=ap;Password=ap"
            : IdentityConnection ?? SqlitePath(IdentityDatabaseFile);

    /// <summary>
    /// Resolves the effective connection string for the federation database, honoring
    /// the configured provider and falling back to a SQLite file in
    /// <see cref="DataDirectory"/>.
    /// </summary>
    public string GetFederationConnectionString() =>
        Provider == DatabaseProvider.Postgresql
            ? FederationConnection ?? "Host=localhost;Database=fediblog_ap;Username=ap;Password=ap"
            : FederationConnection ?? SqlitePath(FederationDatabaseFile);

    private string SqlitePath(string fileName) =>
        $"Data Source={System.IO.Path.Combine(DataDirectory, fileName)}";
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

    /// <summary>
    /// Error handling and dead-letter policy for inbound activity processing
    /// (see <see cref="InboxProcessingOptions"/>).
    /// </summary>
    public InboxProcessingOptions InboxProcessing { get; set; } = new();

    /// <summary>
    /// Federation cache backend configuration (see <see cref="CacheOptions"/>).
    /// </summary>
    public CacheOptions Cache { get; set; } = new();

    /// <summary>
    /// Real-time (SignalR) scale-out configuration (see <see cref="RealtimeOptions"/>).
    /// </summary>
    public RealtimeOptions Realtime { get; set; } = new();

    /// <summary>
    /// Relational database provider and connection strings (see
    /// <see cref="DatabaseOptions"/>).
    /// </summary>
    public DatabaseOptions Database { get; set; } = new();

    /// <summary>
    /// Reverse-proxy (forwarded headers) configuration (see
    /// <see cref="ForwardedHeadersOptions"/>). When the app is deployed behind a
    /// TLS-terminating proxy (e.g. nginx), this makes generated federation URLs
    /// use the public scheme/host instead of the internal one.
    /// </summary>
    public ForwardedHeadersOptions ForwardedHeaders { get; set; } = new();
}

/// <summary>
/// Reverse-proxy (forwarded headers) configuration. When the app runs behind a
/// TLS-terminating reverse proxy (nginx, Caddy, etc.), the proxy rewrites
/// <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c> (or <c>X-Forwarded-For</c>)
/// and the ASP.NET Core Forwarded Headers middleware uses them to populate
/// <c>Request.Scheme</c> / <c>Request.Host</c>. Every generated federation URL
/// (webfinger self-links, actor id/inbox/outbox, delivery targets) is derived
/// from those, so honoring the forwarded headers is what makes an instance
/// deployed behind a proxy advertise <c>https://</c> URLs to remote instances.
/// </summary>
public class ForwardedHeadersOptions
{
    /// <summary>
    /// When true, the app registers the Forwarded Headers middleware. Off by
    /// default so a bare (non-proxied) deployment is unchanged.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Comma-separated CIDR / IP list of trusted proxies allowed to set the
    /// forwarded headers. Defaults to the loopback range, which is correct for
    /// the common "nginx on the same host" deployment. Leave empty to trust all
    /// proxies (only do this if the proxy is on an untrusted network you still
    /// control end-to-end).
    /// </summary>
    public string[] TrustedProxies { get; set; } = { "127.0.0.1", "::1" };
}
