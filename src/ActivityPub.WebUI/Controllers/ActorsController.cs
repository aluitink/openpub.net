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
    public async Task<IActionResult> Outbox(string username, int page = 1)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null) return NotFound();

        var pageSize = 20;
        var skip = (page - 1) * pageSize;
        var activityIds = await _repository.GetActorOutboxActivitiesAsync(username, skip, pageSize);
        var total = activityIds.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollection",
            id = $"https://localhost/actors/outbox/{username}",
            totalItems = total,
            orderedItems = activityIds.Select(id => new { id }).ToList(),
            first = $"https://localhost/actors/outbox/{username}?page=1",
            last = $"https://localhost/actors/outbox/{username}?page={totalPages}"
        };

        return Content(JsonSerializer.Serialize(orderedCollection, JsonOpts), "application/ld+json");
    }

    [HttpGet]
    public async Task<IActionResult> Followers(string username, int page = 1)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null) return NotFound();

        var pageSize = 20;
        var skip = (page - 1) * pageSize;
        var followerIds = await _repository.GetFollowersAsync(username, skip, pageSize);
        var total = followerIds.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollection",
            id = $"https://localhost/actors/followers/{username}",
            totalItems = total,
            orderedItems = followerIds.Select(id => new { id }).ToList(),
            first = $"https://localhost/actors/followers/{username}?page=1",
            last = $"https://localhost/actors/followers/{username}?page={totalPages}"
        };

        return Content(JsonSerializer.Serialize(orderedCollection, JsonOpts), "application/ld+json");
    }

    [HttpGet]
    public async Task<IActionResult> Following(string username, int page = 1)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null) return NotFound();

        var pageSize = 20;
        var skip = (page - 1) * pageSize;
        var followingIds = await _repository.GetFollowingAsync(username, skip, pageSize);
        var total = followingIds.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollection",
            id = $"https://localhost/actors/following/{username}",
            totalItems = total,
            orderedItems = followingIds.Select(id => new { id }).ToList(),
            first = $"https://localhost/actors/following/{username}?page=1",
            last = $"https://localhost/actors/following/{username}?page={totalPages}"
        };

        return Content(JsonSerializer.Serialize(orderedCollection, JsonOpts), "application/ld+json");
    }

    [HttpGet]
    public async Task<IActionResult> Liked(string username, int page = 1)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null) return NotFound();

        var pageSize = 20;
        var skip = (page - 1) * pageSize;
        var likedIds = await _repository.GetLikedActivitiesAsync(username, skip, pageSize);
        var total = likedIds.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollection",
            id = $"https://localhost/actors/liked/{username}",
            totalItems = total,
            orderedItems = likedIds.Select(id => new { id }).ToList(),
            first = $"https://localhost/actors/liked/{username}?page=1",
            last = $"https://localhost/actors/liked/{username}?page={totalPages}"
        };

        return Content(JsonSerializer.Serialize(orderedCollection, JsonOpts), "application/ld+json");
    }
}
