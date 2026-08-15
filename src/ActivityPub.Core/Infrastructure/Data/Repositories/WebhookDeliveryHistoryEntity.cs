namespace ActivityPub.Core.Repositories;

public class WebhookDeliveryHistoryEntity
{
    public string Id { get; set; } = string.Empty;
    public required string DeliveryId { get; set; }
    public required string EventType { get; set; }
    public required string RequestHeaders { get; set; }
    public required string RequestBody { get; set; }
    public required string ResponseHeaders { get; set; }
    public required string ResponseBody { get; set; }
    public int HttpResponseCode { get; set; }
    public DateTime Timestamp { get; set; }
}
