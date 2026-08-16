namespace ActivityPub.Core.Repositories;

public class CommunityEntity
{
    public int Id { get; set; }
    public required string CommunityId { get; set; }
    public required string Name { get; set; }
    public required string JsonData { get; set; }
    public int OwnerActorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ActorEntity? Owner { get; set; }
}

public class CommunityMemberEntity
{
    public int Id { get; set; }
    public int CommunityId { get; set; }
    public int ActorId { get; set; }
    public DateTime JoinedAt { get; set; }

    public CommunityEntity? Community { get; set; }
    public ActorEntity? Actor { get; set; }
}
