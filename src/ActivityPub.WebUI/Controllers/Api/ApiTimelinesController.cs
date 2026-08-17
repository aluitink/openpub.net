using ActivityPub.Core.Interfaces;
using ActivityPub.WebUI.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers.Api;

/// <summary>
/// Local REST API — timeline endpoints, Mastodon-compatible.
/// Home timeline mirrors the WebUI: the union of the actor's outbox and inbox.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = BearerTokenAuthConstants.BothSchemes)]
[Route("api/v1")]
[Produces("application/json")]
public class ApiTimelinesController : ControllerBase
{
    private readonly IActivityPubRepository _repository;

    public ApiTimelinesController(IActivityPubRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [Route("timelines/home")]
    [ResponseCache(Duration = 3, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Home([FromQuery] ulong max_id = 0, [FromQuery] ulong since_id = 0, [FromQuery] int limit = 20)
    {
        var username = User.Identity!.Name!;
        limit = Math.Clamp(limit, 1, 40);

        // The home timeline is the union of outbox and inbox. Both are stored
        // oldest-first; to support max_id (page back) we fetch a generous window
        // then filter to items older than max_id by numeric status ID.
        var window = Math.Max(limit * 2, 40);
        var outboxIds = await _repository.GetActorOutboxActivitiesAsync(username, 0, window);
        var inboxIds = await _repository.GetInboxActivitiesAsync(username, 0, window);

        var allIds = new HashSet<string>(outboxIds);
        allIds.UnionWith(inboxIds);

        var statuses = new List<ApiStatus>();
        foreach (var id in allIds)
        {
            var activity = await _repository.GetActivityAsync(id);
            if (activity == null)
                continue;
            var status = await ApiMapper.ToStatusAsync(_repository, activity, username);
            if (status == null)
                continue;
            if (ulong.TryParse(status.Id, out var num))
            {
                if (max_id > 0 && num >= max_id)
                    continue;
                if (since_id > 0 && num <= since_id)
                    continue;
            }
            statuses.Add(status);
        }

        statuses.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        statuses = statuses.Take(limit).ToList();

        return Ok(statuses);
    }
}
