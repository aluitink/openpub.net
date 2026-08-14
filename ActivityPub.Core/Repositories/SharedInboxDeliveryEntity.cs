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
