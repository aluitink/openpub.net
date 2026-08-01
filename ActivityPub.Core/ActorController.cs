using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using ActivityPub.Core.Infrastructure.Telemetry;
using System.Diagnostics;

namespace ActivityPub.Core;

[ApiController]
[Route("users/{username}")]
public class ActorController : ControllerBase
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<ActorController> _logger;
    private readonly ActivityPubTelemetry _telemetry;

    public ActorController(IActivityPubRepository repository, ILogger<ActorController> logger, ActivityPubTelemetry telemetry)
    {
        _repository = repository;
        _logger = logger;
        _telemetry = telemetry;
    }

    [HttpGet]
    public async Task<IActionResult> GetActor(
        [FromRoute] string username)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Actor request received for username: {Username}", username);
        
        try
        {
            // Validate the username parameter
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Actor request missing username parameter");
                _telemetry.RecordHttpRequestError(Request.Method, Request.Path, 400, new ArgumentException("Username parameter is required"));
                return BadRequest(new { error = "username parameter is required" });
            }

            // Fetch the actor from the repository
            var actor = await _repository.GetUserActorAsync(username);
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                _telemetry.RecordHttpRequestError(Request.Method, Request.Path, 404, new KeyNotFoundException("Actor not found"));
                return NotFound(new { error = "Actor not found" });
            }

            _logger.LogInformation("Actor request successful for username: {Username}", username);
            _telemetry.RecordActivityProcessed("Actor");
            _telemetry.RecordHttpRequestProcessed(Request.Method, Request.Path, 200, stopwatch.ElapsedMilliseconds);
            
            return Ok(actor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Actor request for username: {Username}", username);
            _telemetry.RecordActivityError("Actor", ex);
            _telemetry.RecordHttpRequestError(Request.Method, Request.Path, 500, ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("Actor request completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }
    }
}