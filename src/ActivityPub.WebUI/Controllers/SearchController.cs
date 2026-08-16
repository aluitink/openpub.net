using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Obj = ActivityPub.Core.Models.Object;

namespace ActivityPub.WebUI.Controllers;

public class SearchController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ApplicationDbContext _identityDb;
    private readonly ILogger<SearchController> _logger;

    public SearchController(IActivityPubRepository repository, ApplicationDbContext identityDb, ILogger<SearchController> logger)
    {
        _repository = repository;
        _identityDb = identityDb;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string q = "", string tab = "notes")
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return View(new SearchViewModel
            {
                Query = q,
                Tab = tab,
                Users = new List<SearchUserResult>(),
                Notes = new List<SearchNoteResult>(),
            });
        }

        var model = new SearchViewModel
        {
            Query = q,
            Tab = tab,
        };

        if (tab == "users" || tab == "all")
        {
            var users = await SearchUsersAsync(q);
            model.Users = users;
        }

        if (tab == "notes" || tab == "all")
        {
            var notes = await SearchNotesAsync(q);
            model.Notes = notes;
        }

        if (tab == "hashtags" || tab == "all")
        {
            model.Hashtags = ExtractHashtagsFromNotes(model.Notes);
        }

        return View(model);
    }

    async Task<List<SearchUserResult>> SearchUsersAsync(string query)
    {
        var likeQuery = $"%{query}%";
        try
        {
            var users = await _identityDb.Users
                .Where(u =>
                    (u.UserName != null && EF.Functions.Like(u.UserName, likeQuery)) ||
                    EF.Functions.Like(u.DisplayName, likeQuery) ||
                    (u.ActorId != null && EF.Functions.Like(u.ActorId, likeQuery)))
                .OrderBy(u => u.UserName)
                .Take(20)
                .Select(u => new SearchUserResult
                {
                    Username = u.UserName ?? "",
                    DisplayName = u.DisplayName,
                    Bio = u.Bio,
                    AvatarUrl = u.AvatarUrl,
                    ActorId = u.ActorId,
                })
                .ToListAsync();

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users for query: {Query}", query);
            return new List<SearchUserResult>();
        }
    }

    async Task<List<SearchNoteResult>> SearchNotesAsync(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        var noteResults = new List<SearchNoteResult>();

        var users = await _identityDb.Users.ToListAsync();

        foreach (var user in users)
        {
            if (noteResults.Count >= 20) break;

            if (string.IsNullOrEmpty(user.UserName)) continue;
            var activityIds = await _repository.GetActorOutboxActivitiesAsync(user.UserName, 0, 50);

            foreach (var activityId in activityIds)
            {
                if (noteResults.Count >= 20) break;

                var activity = await _repository.GetActivityAsync(activityId);
                if (activity == null) continue;

                if (activity.Object is not Obj obj) continue;
                if (obj.Type == "Tombstone") continue;

                var content = obj.Content ?? "";
                var name = obj.Name ?? "";

                if (!content.ToLowerInvariant().Contains(lowerQuery) &&
                    !name.ToLowerInvariant().Contains(lowerQuery) &&
                    (obj.Tag == null || !obj.Tag.Any(t => t.ToLowerInvariant().Contains(lowerQuery))))
                {
                    continue;
                }

                var author = await GetAuthorForActivity(activity);

                noteResults.Add(new SearchNoteResult
                {
                    ActivityId = activity.Id ?? "",
                    Content = Truncate(content, 200),
                    AuthorName = author?.Name ?? "Unknown",
                    AuthorUsername = user.UserName,
                    Published = activity.Published ?? DateTime.UtcNow,
                    HasTag = obj.Tag?.Any(t => t.ToLowerInvariant().Contains(lowerQuery)) ?? false,
                });
            }
        }

        noteResults.Sort((a, b) => b.Published.CompareTo(a.Published));
        return noteResults;
    }

    async Task<Actor?> GetAuthorForActivity(Activity activity)
    {
        var actorUrl = activity.ActorId;
        if (string.IsNullOrEmpty(actorUrl)) return null;

        var username = ExtractUsername(actorUrl);
        if (string.IsNullOrEmpty(username)) return null;

        return await _repository.GetUserActorAsync(username);
    }

    static string? ExtractUsername(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var parts = url.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "users" && i + 1 < parts.Length)
                return parts[i + 1];
        }
        return null;
    }

    static List<SearchHashtagResult> ExtractHashtagsFromNotes(List<SearchNoteResult> notes)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var note in notes)
        {
            if (!note.HasTag) continue;
            var regex = new System.Text.RegularExpressions.Regex(@"#(\w+)");
            var matches = regex.Matches(note.Content);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var tag = match.Groups[1].Value;
                counts[tag] = counts.TryGetValue(tag, out var c) ? c + 1 : 1;
            }
        }
        return counts.Select(kv => new SearchHashtagResult { Tag = kv.Key, Count = kv.Value })
            .OrderByDescending(h => h.Count)
            .Take(20)
            .ToList();
    }

    static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }
}

public class SearchViewModel
{
    public string Query { get; set; } = "";
    public string Tab { get; set; } = "notes";
    public List<SearchNoteResult> Notes { get; set; } = new();
    public List<SearchUserResult> Users { get; set; } = new();
    public List<SearchHashtagResult> Hashtags { get; set; } = new();
}

public class SearchNoteResult
{
    public string ActivityId { get; set; } = "";
    public string Content { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorUsername { get; set; } = "";
    public DateTime Published { get; set; }
    public bool HasTag { get; set; }
}

public class SearchUserResult
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string? ActorId { get; set; }
}

public class SearchHashtagResult
{
    public string Tag { get; set; } = "";
    public int Count { get; set; }
}