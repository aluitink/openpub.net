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

    // Base URL for this request, derived from the Host header (with scheme) so
    // the document is valid for whatever domain the instance is deployed under
    // (openpub.luit.ink in production, localhost in tests). Falls back to
    // https://localhost if the host is unavailable.
    private string BaseUrl()
    {
        if (Request != null)
        {
            var scheme = Request.Scheme;
            var host = Request.Host.HasValue ? Request.Host.Value : null;
            if (!string.IsNullOrEmpty(scheme) && !string.IsNullOrEmpty(host))
                return $"{scheme}://{host}";
        }
        return "https://localhost";
    }

    [HttpGet]
    public async Task<IActionResult> Show(string username)
    {
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return NotFound();
        }

        var baseUrl = BaseUrl();
        actor.Context = "https://www.w3.org/ns/activitystreams";
        actor.Type = "Person";
        // The actor's canonical id must be stable and point at the real actor
        // document route (/actors/show/{username}) so remote instances can
        // resolve and match it. The stored id may still carry a legacy
        // localhost prefix, so always normalize it from the live request host.
        actor.Id = $"{baseUrl}/actors/show/{username}";
        actor.Url = $"{baseUrl}/@{username}";
        actor.Inbox = $"{baseUrl}/inbox/{username}";
        actor.Outbox = $"{baseUrl}/actors/outbox/{username}";
        actor.Followers = $"{baseUrl}/actors/followers/{username}";
        actor.Following = $"{baseUrl}/actors/following/{username}";
        actor.Liked = $"{baseUrl}/actors/liked/{username}";
        actor.SharedInbox = $"{baseUrl}/inbox/shared";

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
        var baseUrl = BaseUrl();

        var first = $"{baseUrl}/actors/outbox/{username}?page=1";
        var last = $"{baseUrl}/actors/outbox/{username}?page={totalPages}";
        var prev = page > 1 ? $"{baseUrl}/actors/outbox/{username}?page={page - 1}" : null;
        var next = page < totalPages ? $"{baseUrl}/actors/outbox/{username}?page={page + 1}" : null;

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollectionPage",
            id = $"{baseUrl}/actors/outbox/{username}?page={page}",
            partOf = $"{baseUrl}/actors/outbox/{username}",
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
        var baseUrl = BaseUrl();

        var first = $"{baseUrl}/actors/followers/{username}?page=1";
        var last = $"{baseUrl}/actors/followers/{username}?page={totalPages}";
        var prev = page > 1 ? $"{baseUrl}/actors/followers/{username}?page={page - 1}" : null;
        var next = page < totalPages ? $"{baseUrl}/actors/followers/{username}?page={page + 1}" : null;

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollectionPage",
            id = $"{baseUrl}/actors/followers/{username}?page={page}",
            partOf = $"{baseUrl}/actors/followers/{username}",
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
        var baseUrl = BaseUrl();

        var first = $"{baseUrl}/actors/following/{username}?page=1";
        var last = $"{baseUrl}/actors/following/{username}?page={totalPages}";
        var prev = page > 1 ? $"{baseUrl}/actors/following/{username}?page={page - 1}" : null;
        var next = page < totalPages ? $"{baseUrl}/actors/following/{username}?page={page + 1}" : null;

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollectionPage",
            id = $"{baseUrl}/actors/following/{username}?page={page}",
            partOf = $"{baseUrl}/actors/following/{username}",
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
        var baseUrl = BaseUrl();

        var first = $"{baseUrl}/actors/liked/{username}?page=1";
        var last = $"{baseUrl}/actors/liked/{username}?page={totalPages}";
        var prev = page > 1 ? $"{baseUrl}/actors/liked/{username}?page={page - 1}" : null;
        var next = page < totalPages ? $"{baseUrl}/actors/liked/{username}?page={page + 1}" : null;

        var orderedCollection = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            type = "OrderedCollectionPage",
            id = $"{baseUrl}/actors/liked/{username}?page={page}",
            partOf = $"{baseUrl}/actors/liked/{username}",
            orderedItems = likedIds.Select(id => new { id }).ToList(),
            first,
            last,
            prev,
            next
        };

        return Content(JsonSerializer.Serialize(orderedCollection, JsonOpts), "application/ld+json");
    }
}
