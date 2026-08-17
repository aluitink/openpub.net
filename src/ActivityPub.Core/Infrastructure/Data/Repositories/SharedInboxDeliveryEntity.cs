namespace ActivityPub.Core.Repositories;

public class SharedInboxDeliveryEntity
{
    public string Id { get; set; } = string.Empty;
    public required string ActivityId { get; set; }
    public required string ActivityJson { get; set; }
    public required string TargetActorId { get; set; }
    public DeliveryStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastDeliveryAttempt { get; set; }
    /// <summary>
    /// When the next delivery attempt is allowed, after a failure. A
    /// <c>Failed</c> item is only picked up by the queue processor once this
    /// time has passed (exponential-backoff gating). Null means "immediately
    /// eligible".
    /// </summary>
    public DateTime? NextRetryAt { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum DeliveryStatus
{
    Queued = 0,
    Processing = 1,
    Delivered = 2,
    Failed = 3,
    MaxRetriesExceeded = 4
}
