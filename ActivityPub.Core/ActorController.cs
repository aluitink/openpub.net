using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ActivityPub.Core;

[ApiController]
[Route("users/{username}")]
public class ActorController : ControllerBase
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<ActorController> _logger;

    public ActorController(IActivityPubRepository repository, ILogger<ActorController> logger)
    {
        _repository = repository;
        _logger = logger;
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
                return NotFound(new { error = "Actor not found" });
            }

            _logger.LogInformation("Actor request successful for username: {Username}", username);
            stopwatch.Stop();
            _logger.LogDebug("Actor request completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
            
            return Ok(actor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Actor request for username: {Username}", username);
            throw;
        }
    }

    [HttpGet("outbox")]
    public async Task<IActionResult> GetOutbox(
        [FromRoute] string username)
    {
        _logger.LogInformation("Outbox request received for username: {Username}", username);
        
        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Outbox request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return NotFound(new { error = "Actor not found" });
            }

            var activities = await _repository.GetActorOutboxActivitiesAsync(username, 0, 20);
            
            var orderedCollection = new OrderedCollection
            {
                Id = $"{actor.Id}/outbox",
                Type = "OrderedCollection",
                TotalItems = activities.Count,
                OrderedItems = activities
            };

            _logger.LogInformation("Outbox request successful for username: {Username}", username);
            
            return Ok(orderedCollection);
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
                return BadRequest(new { error = "activity body is required" });
            }

            await _repository.SaveActivityAsync(activity);
            
            _logger.LogInformation("Outbox post successful for username: {Username}, activity: {ActivityType}", 
                username, activity.Type);

            return Ok(new { success = true, activityId = activity.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Outbox post for username: {Username}", username);
            throw;
        }
    }

    [HttpGet("followers")]
    public async Task<IActionResult> GetFollowers(
        [FromRoute] string username)
    {
        _logger.LogInformation("Followers request received for username: {Username}", username);
        
        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Followers request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return NotFound(new { error = "Actor not found" });
            }

            var followers = await _repository.GetFollowersAsync(username, 0, 20);
            
            var collection = new Collection
            {
                Id = $"{actor.Id}/followers",
                Type = "Collection",
                TotalItems = followers.Count,
                Items = followers
            };

            _logger.LogInformation("Followers request successful for username: {Username}", username);
            
            return Ok(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Followers request for username: {Username}", username);
            throw;
        }
    }

    [HttpGet("following")]
    public async Task<IActionResult> GetFollowing(
        [FromRoute] string username)
    {
        _logger.LogInformation("Following request received for username: {Username}", username);
        
        try
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Following request missing username parameter");
                return BadRequest(new { error = "username parameter is required" });
            }

            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return NotFound(new { error = "Actor not found" });
            }

            var following = await _repository.GetFollowingAsync(username, 0, 20);
            
            var collection = new Collection
            {
                Id = $"{actor.Id}/following",
                Type = "Collection",
                TotalItems = following.Count,
                Items = following
            };

            _logger.LogInformation("Following request successful for username: {Username}", username);
            
            return Ok(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Following request for username: {Username}", username);
            throw;
        }
    }
}
