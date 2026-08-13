using System.Text.Json.Serialization;

namespace ActivityPub.Core.Repositories;

public class ActorEntity
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string JsonData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
