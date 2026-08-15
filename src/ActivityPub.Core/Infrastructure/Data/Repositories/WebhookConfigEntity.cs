namespace ActivityPub.Core.Repositories;

public class WebhookConfigEntity
{
    public int Id { get; set; }
    public required string ActorId { get; set; }
    public WebhookDeliveryMethod DeliveryMethod { get; set; }
    public required string EndpointUrl { get; set; }
    public required string HttpMethod { get; set; }
    public bool Enabled { get; set; }
    public string? SecretKey { get; set; }
    public int MaxRetries { get; set; }
    public int RetryDelaySeconds { get; set; }
    public bool UseExponentialBackoff { get; set; }
    public required string EventType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum WebhookDeliveryMethod
{
    HttpPost = 0,
    HttpPut = 1,
    WebhookRelay = 2
}
