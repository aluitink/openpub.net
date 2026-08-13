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
}