using ActivityPub.Core.Models;

namespace ActivityPub.Core.Interfaces;

public interface ICommunityService
{
    Task<Community?> CreateCommunityAsync(string ownerId, string name, string? summary, CancellationToken cancellationToken = default);
    Task<bool> UpdateCommunityAsync(Community community, CancellationToken cancellationToken = default);
    Task<Community?> GetCommunityByIdAsync(string communityId, CancellationToken cancellationToken = default);
    Task<ICollection<Community>> GetAllCommunitiesAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default);
    Task<bool> JoinCommunityAsync(string actorId, string communityId, CancellationToken cancellationToken = default);
    Task<bool> LeaveCommunityAsync(string actorId, string communityId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(string actorId, string communityId, CancellationToken cancellationToken = default);
    Task<ICollection<string>> GetMemberIdsAsync(string communityId, CancellationToken cancellationToken = default);
    Task<int> GetMemberCountAsync(string communityId, CancellationToken cancellationToken = default);
    Task<ICollection<Community>> GetMyCommunitiesAsync(string actorId, CancellationToken cancellationToken = default);
    Task<ICollection<Community>> SearchCommunitiesAsync(string query, CancellationToken cancellationToken = default);
    Task<bool> DeleteCommunityAsync(string ownerId, string communityId, CancellationToken cancellationToken = default);
}
