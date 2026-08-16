namespace ActivityPub.Core.Repositories;

public class UserPreferenceEntity
{
    public int Id { get; set; }
    public int ActorId { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime CreatedAt { get; set; }

    public ActorEntity? Actor { get; set; }
}
