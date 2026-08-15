namespace ActivityPub.Core.Repositories;

public class WebhookDeliveryEntity
{
    public string Id { get; set; } = string.Empty;
    public required string ConfigId { get; set; }
    public required string ActivityId { get; set; }
    public required string ActivityJson { get; set; }
    public required string ActorId { get; set; }
    public WebhookDeliveryStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastDeliveryAttempt { get; set; }
    public string? FailureReason { get; set; }
    public int? HttpResponseCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum WebhookDeliveryStatus
{
    Queued = 0,
    Processing = 1,
    Delivered = 2,
    Failed = 3,
    MaxRetriesExceeded = 4
}
