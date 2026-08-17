using System.Text.Json;
using ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers.Api;

/// <summary>
/// Local REST API — status (note) endpoints, Mastodon-compatible.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class ApiStatusesController : ControllerBase
{
    private readonly IActivityPubRepository _repository;

    public ApiStatusesController(IActivityPubRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("statuses")]
    [ResponseCache(Duration = 3, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Timelines(
        [FromQuery] string? account,
        [FromQuery] ulong max_id = 0,
        [FromQuery] ulong since_id = 0,
        [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(account))
            return BadRequest(new { error = "The 'account' query parameter is required." });

        limit = Math.Clamp(limit, 1, 40);

        // Page forward to the activity that max_id refers to (oldest-first outbox),
        // then take the next `limit` items (i.e. older than max_id).
        var skip = 0;
        if (max_id > 0)
        {
            var allIds = (await _repository.GetActorOutboxActivitiesAsync(account, 0, 200)).ToList();
            var idx = allIds.FindIndex(id => ApiMapper.ToApiStatusId(id) == max_id.ToString());
            if (idx >= 0)
                skip = idx + 1;
        }

        var ids = await _repository.GetActorOutboxActivitiesAsync(account, skip, limit);
        var statuses = new List<ApiStatus>();
        var viewer = User.Identity?.Name;
        foreach (var id in ids)
        {
            var activity = await _repository.GetActivityAsync(id);
            if (activity == null)
                continue;
            var status = await ApiMapper.ToStatusAsync(_repository, activity, viewer);
            if (status == null)
                continue;
            if (since_id > 0 && ulong.TryParse(status.Id, out var num) && num <= since_id)
                continue;
            statuses.Add(status);
        }

        return Ok(statuses);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("statuses/{id}")]
    public async Task<IActionResult> Show(string id)
    {
        var activity = await ResolveActivityAsync(id);
        if (activity == null)
            return NotFound(new { error = "Status not found." });

        var viewer = User.Identity?.Name;
        var status = await ApiMapper.ToStatusAsync(_repository, activity, viewer);
        if (status == null)
            return NotFound(new { error = "Status not found." });

        return Ok(status);
    }

    [HttpDelete]
    [Authorize]
    [Route("statuses/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var activity = await ResolveActivityAsync(id);
        if (activity == null)
            return NotFound(new { error = "Status not found." });

        var deleted = await _repository.DeleteActivityAsync(activity.Id);
        if (!deleted)
            return NotFound(new { error = "Status not found." });

        return NoContent();
    }

    /// <summary>
    /// Resolve an activity by numeric API ID (snowflake-style) or by its full
    /// ActivityPub URL.
    /// </summary>
    private async Task<ActivityPub.Core.Models.Activity?> ResolveActivityAsync(string id)
    {
        var byUrl = await _repository.GetActivityAsync(id);
        if (byUrl != null)
            return byUrl;

        if (ulong.TryParse(id, out var numericId))
        {
            var allIds = await _repository.GetAllActivityIdsAsync();
            foreach (var url in allIds)
            {
                if (ApiMapper.ToApiStatusId(url) == id)
                {
                    var activity = await _repository.GetActivityAsync(url);
                    if (activity != null)
                        return activity;
                }
            }
        }

        return null;
    }
}
