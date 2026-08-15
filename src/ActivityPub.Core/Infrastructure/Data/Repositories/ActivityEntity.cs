namespace ActivityPub.Core.Repositories;

public class ActivityEntity
{
    public int Id { get; set; }
    public required string ActivityId { get; set; }
    public required string JsonData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
