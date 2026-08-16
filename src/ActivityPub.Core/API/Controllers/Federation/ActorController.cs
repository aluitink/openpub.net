using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ActivityPub.Core.API.Controllers.Federation;

[ApiController]
[Route("users/{username}")]
public class ActorController : ControllerBase
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<ActorController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ActivityPubOptions _options;

    public ActorController(IActivityPubRepository repository, ILogger<ActorController> logger, IOptions<ActivityPubOptions> options)
    {
        _repository = repository;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetActor(
        [FromRoute] string username)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Actor request received for username: {Username}", username);

        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Actor request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return new ContentResult { StatusCode = 404, Content = "" };
            }

            if (actor.PublicKey == null)
            {
                actor.PublicKey = new PublicKey
                {
                    Id = $"{actor.Id}/#main-key",
                    Owner = actor.Id,
                    PublicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0Z3VS5JJcds3xfn/ygWe\n-----END PUBLIC KEY-----"
                };
            }

            _logger.LogInformation("Actor request successful for username: {Username}", username);
            stopwatch.Stop();
            _logger.LogDebug("Actor request completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

            var json = JsonSerializer.Serialize(actor, _jsonOptions);
            return Content(json, "application/activity+json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Actor request for username: {Username}", username);
            throw;
        }
    }

    [HttpGet("outbox")]
    public async Task<IActionResult> GetOutbox(
        [FromRoute] string username,
        [FromQuery] int page = 0,
        [FromQuery] int limit = 20)
    {
        _logger.LogInformation("Outbox request received for username: {Username}, page: {Page}, limit: {Limit}", username, page, limit);

        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Outbox request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            limit = Math.Max(1, Math.Min(limit, 100));
            var skip = page * limit;

            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return new ContentResult { StatusCode = 404, Content = "" };
            }

            var activities = await _repository.GetActorOutboxActivitiesAsync(username, skip, limit);

            var orderedCollection = new OrderedCollection
            {
                Id = $"{actor.Id}/outbox",
                Type = "OrderedCollection",
                TotalItems = activities.Count,
                OrderedItems = activities
            };

            _logger.LogInformation("Outbox request successful for username: {Username}", username);

            var json = JsonSerializer.Serialize(orderedCollection, _jsonOptions);
            return Content(json, "application/activity+json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Outbox request for username: {Username}", username);
            throw;
        }
    }

    [HttpPost("outbox")]
    public async Task<IActionResult> PostOutbox(
        [FromRoute] string username,
        [FromBody] global::ActivityPub.Core.Models.Activity activity)
    {
        _logger.LogInformation("Outbox post request received for username: {Username}", username);

        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Outbox post request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            if (activity == null)
            {
                _logger.LogWarning("Outbox post request missing activity body");
                return BadRequest(new ContentResult { StatusCode = 400, Content = "{\"error\":\"activity body is required\"}" });
            }

            await _repository.SaveActivityAsync(activity);

            _logger.LogInformation("Outbox post successful for username: {Username}, activity: {ActivityType}",
                username, activity.Type);

            var resultJson = JsonSerializer.Serialize(new { success = true, activityId = activity.Id }, _jsonOptions);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Outbox post for username: {Username}", username);
            throw;
        }
    }

    [HttpGet("followers")]
    public async Task<IActionResult> GetFollowers(
        [FromRoute] string username,
        [FromQuery] int page = 0,
        [FromQuery] int limit = 20)
    {
        _logger.LogInformation("Followers request received for username: {Username}, page: {Page}, limit: {Limit}", username, page, limit);

        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Followers request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            limit = Math.Max(1, Math.Min(limit, 100));
            var skip = page * limit;

            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return new ContentResult { StatusCode = 404, Content = "" };
            }

            var followers = await _repository.GetFollowersAsync(username, skip, limit);

            var collection = new Collection
            {
                Id = $"{actor.Id}/followers",
                Type = "Collection",
                TotalItems = followers.Count,
                Items = followers
            };

            _logger.LogInformation("Followers request successful for username: {Username}", username);

            var json = JsonSerializer.Serialize(collection, _jsonOptions);
            return Content(json, "application/activity+json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Followers request for username: {Username}", username);
            throw;
        }
    }

    [HttpGet("following")]
    public async Task<IActionResult> GetFollowing(
        [FromRoute] string username,
        [FromQuery] int page = 0,
        [FromQuery] int limit = 20)
    {
        _logger.LogInformation("Following request received for username: {Username}, page: {Page}, limit: {Limit}", username, page, limit);

        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Following request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            limit = Math.Max(1, Math.Min(limit, 100));
            var skip = page * limit;

            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return new ContentResult { StatusCode = 404, Content = "" };
            }

            var following = await _repository.GetFollowingAsync(username, skip, limit);

            var collection = new Collection
            {
                Id = $"{actor.Id}/following",
                Type = "Collection",
                TotalItems = following.Count,
                Items = following
            };

            _logger.LogInformation("Following request successful for username: {Username}", username);

            var json = JsonSerializer.Serialize(collection, _jsonOptions);
            return Content(json, "application/activity+json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Following request for username: {Username}", username);
            throw;
        }
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox(
        [FromRoute] string username,
        [FromQuery] int page = 0,
        [FromQuery] int limit = 20)
    {
        _logger.LogInformation("Inbox request received for username: {Username}, page: {Page}, limit: {Limit}", username, page, limit);

        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Inbox request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            limit = Math.Max(1, Math.Min(limit, 100));
            var skip = page * limit;

            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return new ContentResult { StatusCode = 404, Content = "" };
            }

            var activities = await _repository.GetInboxActivitiesAsync(username, skip, limit);

            var orderedCollection = new OrderedCollection
            {
                Id = $"{actor.Id}/inbox",
                Type = "OrderedCollection",
                TotalItems = activities.Count,
                OrderedItems = activities
            };

            _logger.LogInformation("Inbox request successful for username: {Username}", username);

            var json = JsonSerializer.Serialize(orderedCollection, _jsonOptions);
            return Content(json, "application/activity+json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Inbox request for username: {Username}", username);
            throw;
        }
    }

    [HttpGet("liked")]
    public async Task<IActionResult> GetLiked(
        [FromRoute] string username,
        [FromQuery] int page = 0,
        [FromQuery] int limit = 20)
    {
        _logger.LogInformation("Liked request received for username: {Username}, page: {Page}, limit: {Limit}", username, page, limit);

        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Liked request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            limit = Math.Max(1, Math.Min(limit, 100));
            var skip = page * limit;

            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return new ContentResult { StatusCode = 404, Content = "" };
            }

            var liked = await _repository.GetLikedActivitiesAsync(username, skip, limit);

            var collection = new Collection
            {
                Id = $"{actor.Id}/liked",
                Type = "Collection",
                TotalItems = liked.Count,
                Items = liked
            };

            _logger.LogInformation("Liked request successful for username: {Username}", username);

            var json = JsonSerializer.Serialize(collection, _jsonOptions);
            return Content(json, "application/activity+json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Liked request for username: {Username}", username);
            throw;
        }
    }

    [HttpPost("inbox")]
    public async Task<IActionResult> PostInbox(
        [FromRoute] string username,
        [FromBody] global::ActivityPub.Core.Models.Activity activity)
    {
        _logger.LogInformation("Inbox post request received for username: {Username}", username);

        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Inbox post request missing username parameter");
                return BadRequest("{\"error\":\"username parameter is required\"}");
            }

            if (activity == null)
            {
                _logger.LogWarning("Inbox post request missing activity body");
                return BadRequest("{\"error\":\"activity body is required\"}");
            }

            using var scope = HttpContext.RequestServices.CreateScope();
            var sharedInboxService = scope.ServiceProvider.GetRequiredService<ISharedInboxService>();

            var success = await sharedInboxService.ProcessAndDistributeActivityAsync(username, activity);

            if (success)
            {
                _logger.LogInformation("Inbox post successful for username: {Username}, activity: {ActivityType}",
                    username, activity.Type);
                return Content("{\"success\":true}", "application/json");
            }
            else
            {
                _logger.LogWarning("Inbox post processing failed for username: {Username}", username);
                return BadRequest("{\"error\":\"Failed to process activity\"}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Inbox post for username: {Username}", username);
            return BadRequest("{\"error\":\"Failed to process activity\"}");
        }
    }
}
