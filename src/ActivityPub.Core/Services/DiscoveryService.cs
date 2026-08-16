using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ActivityPub.Core.Interfaces;

public interface IDiscoveryService
{
    Task<ICollection<TrendingHashtag>> GetTrendingHashtagsAsync(TimeSpan? timeWindow = null, int limit = 20, CancellationToken cancellationToken = default);
    Task<ICollection<string>> GetFollowerSuggestionsAsync(string currentUserId, int limit = 10, CancellationToken cancellationToken = default);
    Task<bool> IsMutedAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default);
    Task<bool> IsContentFilteredAsync(string currentUserId, string content, CancellationToken cancellationToken = default);
    Task AddMutedUserAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default);
    Task RemoveMutedUserAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default);
    Task<ICollection<string>> GetMutedUsersAsync(string currentUserId, CancellationToken cancellationToken = default);
    Task AddContentFilterAsync(string currentUserId, string filterKeyword, CancellationToken cancellationToken = default);
    Task RemoveContentFilterAsync(string currentUserId, string filterKeyword, CancellationToken cancellationToken = default);
    Task<ICollection<string>> GetContentFiltersAsync(string currentUserId, CancellationToken cancellationToken = default);
}

public record TrendingHashtag(string Tag, int Count, DateTime? LastUsed);
