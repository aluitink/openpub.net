using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Obj = ActivityPub.Core.Models.Object;

namespace ActivityPub.WebUI.Controllers;

[Route("hashtag/{tag}")]
[Route("hashtag/{tag}/page/{page:int}")]
public class HashtagController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ApplicationDbContext _identityDb;
    private readonly ILogger<HashtagController> _logger;

    public HashtagController(IActivityPubRepository repository, ApplicationDbContext identityDb, ILogger<HashtagController> logger)
    {
        _repository = repository;
        _identityDb = identityDb;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string tag, int page = 1)
    {
        if (string.IsNullOrEmpty(tag))
            return NotFound();

        if (!tag.StartsWith("#"))
            tag = "#" + tag;

        var lowerTag = tag.ToLowerInvariant();
        var pageSize = 20;
        var skip = (page - 1) * pageSize;
        var notes = new List<HashtagNoteItem>();

        var users = await _identityDb.Users.ToListAsync();

        foreach (var user in users)
        {
            if (notes.Count >= pageSize + 5) break;

            if (string.IsNullOrEmpty(user.UserName)) continue;
            var activityIds = await _repository.GetActorOutboxActivitiesAsync(user.UserName, 0, 100);

            foreach (var activityId in activityIds)
            {
                var activity = await _repository.GetActivityAsync(activityId);
                if (activity == null) continue;

                if (activity.Object is not Obj obj) continue;
                if (obj.Type == "Tombstone") continue;

                var content = obj.Content ?? "";
                var contentLower = content.ToLowerInvariant();

                var match = false;

                if (obj.Tag != null && obj.Tag.Any(t => t.ToLowerInvariant() == lowerTag))
                    match = true;

                if (!match)
                {
                    var words = contentLower.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Any(w => w == lowerTag))
                        match = true;
                }

                if (!match) continue;

                var author = await _repository.GetUserActorAsync(user.UserName);

                notes.Add(new HashtagNoteItem
                {
                    ActivityId = activity.Id ?? "",
                    Content = Truncate(content, 300),
                    AuthorName = author?.Name ?? "Unknown",
                    AuthorUsername = user.UserName,
                    Published = activity.Published ?? DateTime.UtcNow,
                });
            }
        }

        notes.Sort((a, b) => b.Published.CompareTo(a.Published));
        var pagedNotes = notes.Skip(skip).Take(pageSize).ToList();

        return View(new HashtagViewModel
        {
            Tag = tag,
            Notes = pagedNotes,
            Page = page,
            HasMore = notes.Count > page * pageSize,
        });
    }

    static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }
}

public class HashtagViewModel
{
    public string Tag { get; set; } = "";
    public List<HashtagNoteItem> Notes { get; set; } = new();
    public int Page { get; set; }
    public bool HasMore { get; set; }
}

public class HashtagNoteItem
{
    public string ActivityId { get; set; } = "";
    public string Content { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorUsername { get; set; } = "";
    public DateTime Published { get; set; }
}