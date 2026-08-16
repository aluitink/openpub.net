using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ActivityPub.Core.Services;

public class DiscoveryServiceImpl : IDiscoveryService
{
    private readonly ActivityPubDbContext _context;

    public DiscoveryServiceImpl(ActivityPubDbContext context)
    {
        _context = context;
    }

    public async Task<ICollection<TrendingHashtag>> GetTrendingHashtagsAsync(
        TimeSpan? timeWindow = null, int limit = 20, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = timeWindow.HasValue ? now - timeWindow.Value : DateTime.MinValue;

        var jsonDatas = await _context.Activities
            .Where(a => a.CreatedAt >= cutoff)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.JsonData)
            .ToListAsync(cancellationToken);

        var counts = new Dictionary<string, int>();

        foreach (var json in jsonDatas)
        {
            var tags = ExtractHashtagsFromJson(json);
            foreach (var tag in tags)
            {
                counts.TryGetValue(tag, out var c);
                counts[tag] = c + 1;
            }
        }

        return counts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .Take(limit)
            .Select(kvp => new TrendingHashtag(kvp.Key, kvp.Value, null))
            .ToList();
    }

    public async Task<ICollection<string>> GetFollowerSuggestionsAsync(
        string currentUserId, int limit = 10, CancellationToken cancellationToken = default)
    {
        var followingIds = new HashSet<string>();

        var followOut = await _context.Activities
            .Where(a => a.JsonData.Contains("\"type\":\"Follow\"") && a.JsonData.Contains($"\"actor\":\"{currentUserId}\""))
            .Select(a => a.JsonData)
            .ToListAsync(cancellationToken);

        foreach (var json in followOut)
        {
            var obj = ExtractJsonValue(json, "\"object\":\"");
            if (obj != null)
                followingIds.Add(obj);
        }

        var currentActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{currentUserId}\""), cancellationToken);

        if (currentActor == null)
            return new List<string>();

        var allActors = await _context.Actors
            .ToListAsync(cancellationToken);

        var suggestions = new Dictionary<string, int>();

        foreach (var actor in allActors)
        {
            var actorId = ExtractJsonValue(actor.JsonData, "\"id\":\"");
            if (actorId == null) continue;
            if (actor.Id == currentActor.Id) continue;
            if (followingIds.Contains(actorId)) continue;

            var followerCount = await _context.Activities
                .Where(a => a.JsonData.Contains("\"type\":\"Follow\"") && a.JsonData.Contains($"\"object\":\"{actorId}\""))
                .CountAsync(cancellationToken);

            if (followerCount > 0)
                suggestions[actorId] = followerCount;
        }

        return suggestions
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .Take(limit)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public async Task<bool> IsMutedAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default)
    {
        var currentActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{currentUserId}\""), cancellationToken);

        if (currentActor == null) return false;

        return await _context.UserPreferences
            .AnyAsync(p => p.ActorId == currentActor.Id &&
                           p.Key == "muted" &&
                           p.Value == targetUserId, cancellationToken);
    }

    public async Task<bool> IsContentFilteredAsync(string currentUserId, string content, CancellationToken cancellationToken = default)
    {
        var currentActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{currentUserId}\""), cancellationToken);

        if (currentActor == null) return false;

        var filters = await _context.UserPreferences
            .Where(p => p.ActorId == currentActor.Id && p.Key == "filter")
            .Select(p => p.Value)
            .ToListAsync(cancellationToken);

        if (!filters.Any()) return false;

        var lower = content.ToLowerInvariant();
        return filters.Any(f => lower.Contains(f));
    }

    public async Task AddMutedUserAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default)
    {
        var currentActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{currentUserId}\""), cancellationToken);

        if (currentActor == null) return;

        var exists = await _context.UserPreferences
            .AnyAsync(p => p.ActorId == currentActor.Id && p.Key == "muted" && p.Value == targetUserId, cancellationToken);

        if (!exists)
        {
            _context.UserPreferences.Add(new UserPreferenceEntity
            {
                ActorId = currentActor.Id,
                Key = "muted",
                Value = targetUserId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveMutedUserAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default)
    {
        var currentActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{currentUserId}\""), cancellationToken);

        if (currentActor == null) return;

        var pref = await _context.UserPreferences
            .FirstOrDefaultAsync(p => p.ActorId == currentActor.Id && p.Key == "muted" && p.Value == targetUserId, cancellationToken);

        if (pref != null)
        {
            _context.UserPreferences.Remove(pref);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<ICollection<string>> GetMutedUsersAsync(string currentUserId, CancellationToken cancellationToken = default)
    {
        var currentActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{currentUserId}\""), cancellationToken);

        if (currentActor == null) return new List<string>();

        return await _context.UserPreferences
            .Where(p => p.ActorId == currentActor.Id && p.Key == "muted")
            .Select(p => p.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task AddContentFilterAsync(string currentUserId, string filterKeyword, CancellationToken cancellationToken = default)
    {
        var currentActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{currentUserId}\""), cancellationToken);

        if (currentActor == null) return;

        var lowerKeyword = filterKeyword.ToLowerInvariant();

        var exists = await _context.UserPreferences
            .AnyAsync(p => p.ActorId == currentActor.Id &&
                           p.Key == "filter" &&
                           p.Value == lowerKeyword, cancellationToken);

        if (!exists)
        {
            _context.UserPreferences.Add(new UserPreferenceEntity
            {
                ActorId = currentActor.Id,
                Key = "filter",
                Value = lowerKeyword,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveContentFilterAsync(string currentUserId, string filterKeyword, CancellationToken cancellationToken = default)
    {
        var currentActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{currentUserId}\""), cancellationToken);

        if (currentActor == null) return;

        var lowerKeyword = filterKeyword.ToLowerInvariant();

        var pref = await _context.UserPreferences
            .FirstOrDefaultAsync(p => p.ActorId == currentActor.Id &&
                                      p.Key == "filter" &&
                                      p.Value == lowerKeyword, cancellationToken);

        if (pref != null)
        {
            _context.UserPreferences.Remove(pref);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<ICollection<string>> GetContentFiltersAsync(string currentUserId, CancellationToken cancellationToken = default)
    {
        var currentActor = await _context.Actors
            .FirstOrDefaultAsync(a => a.JsonData.Contains($"\"id\":\"{currentUserId}\""), cancellationToken);

        if (currentActor == null) return new List<string>();

        return await _context.UserPreferences
            .Where(p => p.ActorId == currentActor.Id && p.Key == "filter")
            .Select(p => p.Value)
            .ToListAsync(cancellationToken);
    }

    private static HashSet<string> ExtractHashtagsFromJson(string json)
    {
        var hashtags = new HashSet<string>();

        try
        {
            const string tagListKey = "\"tag\":[";

            var contentIdx = json.IndexOf("\"content\":\"", StringComparison.Ordinal);

            if (contentIdx >= 0)
            {
                var contentStart = contentIdx + "\"content\":\"".Length;
                var contentEnd = FindJsonStringEnd(json, contentStart);
                if (contentEnd > contentStart)
                {
                    var content = json.Substring(contentStart, contentEnd - contentStart);
                    var words = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var word in words)
                    {
                        if (word.StartsWith("#"))
                        {
                            var cleaned = word.Substring(1).Trim('!', '.', ',', ';', ':', ')');
                            if (cleaned.Length > 0 && !cleaned.Contains("/"))
                                hashtags.Add("#" + cleaned.ToLowerInvariant());
                        }
                    }
                }
            }

            var tagStart = json.IndexOf(tagListKey, StringComparison.Ordinal);
            if (tagStart >= 0)
            {
                var arrStart = tagStart + tagListKey.Length;
                var arrEnd = FindMatchingBracket(json, arrStart, '[', ']');
                if (arrEnd > arrStart)
                {
                    var arrContent = json.Substring(arrStart, arrEnd - arrStart);
                    var parts = arrContent.Split('"');
                    for (var i = 1; i < parts.Length; i += 2)
                    {
                        var tag = parts[i].Trim();
                        if (tag.Length > 1 && !tag.Contains("/"))
                        {
                            var clean = tag.StartsWith("#") ? tag : "#" + tag;
                            hashtags.Add(clean.ToLowerInvariant());
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return hashtags;
    }

    private static int FindJsonStringEnd(string json, int start)
    {
        for (var i = start; i < json.Length; i++)
        {
            if (json[i] == '\\')
            {
                i++;
                continue;
            }
            if (json[i] == '"')
                return i;
        }
        return json.Length;
    }

    private static int FindMatchingBracket(string json, int start, char open, char close)
    {
        var depth = 0;
        var inString = false;

        for (var i = start; i < json.Length; i++)
        {
            if (json[i] == '\\' && inString)
            {
                i++;
                continue;
            }

            if (json[i] == '"')
                inString = !inString;

            if (!inString)
            {
                if (json[i] == open) depth++;
                if (json[i] == close)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
        }

        return json.Length;
    }

    private static string? ExtractJsonValue(string json, string prefix)
    {
        var idx = json.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return null;

        var start = idx + prefix.Length;
        var end = FindJsonStringEnd(json, start);
        if (end <= start) return null;

        return json.Substring(start, end - start);
    }
}
