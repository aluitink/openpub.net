using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActivityPub.WebUI.Controllers;

[Route("[controller]/[action]/{username}")]
public class ActorsController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<ActorsController> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    public ActorsController(IActivityPubRepository repository, ILogger<ActorsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Show(string username)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return NotFound();
        }

        actor.Context = "https://www.w3.org/ns/activitystreams";
        actor.Type = "Person";
        actor.Url = $"https://localhost/users/{username}";
        actor.Inbox = $"https://localhost/inbox/{username}";
        actor.Outbox = $"https://localhost/actors/outbox/{username}";
        actor.Followers = $"https://localhost/actors/followers/{username}";
        actor.Following = $"https://localhost/actors/following/{username}";
        actor.Liked = $"https://localhost/actors/liked/{username}";
        actor.SharedInbox = "https://localhost/inbox/shared";

        var json = JsonSerializer.Serialize(actor, JsonOpts);
        return Content(json, "application/ld+json");
    }

    [HttpGet]
    [ResponseCache(Duration = 5, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Outbox(string username, int page = 1)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null) return NotFound();

        var pageSize = 20;
        var skip = (page - 1) * pageSize;
        var activityIds = await _repository.GetActorOutboxActivitiesAsync(username, skip, pageSize);
        var total = activityIds.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));

        var first = $"https://localhost/actors/outbox/{username}?page=1";
        var last = $"https://localhost/actors/outbox/{username}?page={totalPages}";
        var prev = page > 1 ? $"https://localhost/actors/outbox/{username}?page={page - 1}" : null;
        var next = page < totalPages ? $"https://localhost/actors/outbox/{username}?page={page + 1}" : null;

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollectionPage",
            id = $"https://localhost/actors/outbox/{username}?page={page}",
            partOf = $"https://localhost/actors/outbox/{username}",
            orderedItems = activityIds.Select(id => new { id }).ToList(),
            first,
            last,
            prev,
            next
        };

        return Content(JsonSerializer.Serialize(orderedCollection, JsonOpts), "application/ld+json");
    }

    [HttpGet]
    [ResponseCache(Duration = 5, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Followers(string username, int page = 1)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null) return NotFound();

        var pageSize = 20;
        var skip = (page - 1) * pageSize;
        var followerIds = await _repository.GetFollowersAsync(username, skip, pageSize);
        var total = followerIds.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));

        var first = $"https://localhost/actors/followers/{username}?page=1";
        var last = $"https://localhost/actors/followers/{username}?page={totalPages}";
        var prev = page > 1 ? $"https://localhost/actors/followers/{username}?page={page - 1}" : null;
        var next = page < totalPages ? $"https://localhost/actors/followers/{username}?page={page + 1}" : null;

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollectionPage",
            id = $"https://localhost/actors/followers/{username}?page={page}",
            partOf = $"https://localhost/actors/followers/{username}",
            orderedItems = followerIds.Select(id => new { id }).ToList(),
            first,
            last,
            prev,
            next
        };

        return Content(JsonSerializer.Serialize(orderedCollection, JsonOpts), "application/ld+json");
    }

    [HttpGet]
    [ResponseCache(Duration = 5, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Following(string username, int page = 1)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null) return NotFound();

        var pageSize = 20;
        var skip = (page - 1) * pageSize;
        var followingIds = await _repository.GetFollowingAsync(username, skip, pageSize);
        var total = followingIds.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));

        var first = $"https://localhost/actors/following/{username}?page=1";
        var last = $"https://localhost/actors/following/{username}?page={totalPages}";
        var prev = page > 1 ? $"https://localhost/actors/following/{username}?page={page - 1}" : null;
        var next = page < totalPages ? $"https://localhost/actors/following/{username}?page={page + 1}" : null;

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollectionPage",
            id = $"https://localhost/actors/following/{username}?page={page}",
            partOf = $"https://localhost/actors/following/{username}",
            orderedItems = followingIds.Select(id => new { id }).ToList(),
            first,
            last,
            prev,
            next
        };

        return Content(JsonSerializer.Serialize(orderedCollection, JsonOpts), "application/ld+json");
    }

    [HttpGet]
    [ResponseCache(Duration = 5, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Liked(string username, int page = 1)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null) return NotFound();

        var pageSize = 20;
        var skip = (page - 1) * pageSize;
        var likedIds = await _repository.GetLikedActivitiesAsync(username, skip, pageSize);
        var total = likedIds.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));

        var first = $"https://localhost/actors/liked/{username}?page=1";
        var last = $"https://localhost/actors/liked/{username}?page={totalPages}";
        var prev = page > 1 ? $"https://localhost/actors/liked/{username}?page={page - 1}" : null;
        var next = page < totalPages ? $"https://localhost/actors/liked/{username}?page={page + 1}" : null;

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollectionPage",
            id = $"https://localhost/actors/liked/{username}?page={page}",
            partOf = $"https://localhost/actors/liked/{username}",
            orderedItems = likedIds.Select(id => new { id }).ToList(),
            first,
            last,
            prev,
            next
        };

        return Content(JsonSerializer.Serialize(orderedCollection, JsonOpts), "application/ld+json");
    }
}
