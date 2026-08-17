using ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers.Api;

/// <summary>
/// Local REST API — account endpoints, Mastodon-compatible.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class ApiAccountsController : ControllerBase
{
    private readonly IActivityPubRepository _repository;

    public ApiAccountsController(IActivityPubRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("accounts")]
    [ResponseCache(Duration = 3, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Lookup([FromQuery] string? acct)
    {
        if (string.IsNullOrWhiteSpace(acct))
            return BadRequest(new { error = "The 'acct' query parameter is required." });

        var username = acct.Contains('@') ? acct.Split('@')[0] : acct;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
            return NotFound(new { error = "Account not found." });

        var account = ApiMapper.ToAccount(actor);
        if (account == null)
            return NotFound(new { error = "Account not found." });

        account.FollowersCount = await _repository.GetFollowerCountAsync(username);
        account.FollowingCount = await _repository.GetFollowingCountAsync(username);
        account.StatusesCount = await _repository.GetNoteCountAsync(username);

        return Ok(account);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("accounts/{id}")]
    public async Task<IActionResult> Show(string id)
    {
        var actor = await ResolveActorAsync(id);
        if (actor == null)
            return NotFound(new { error = "Account not found." });

        var username = actor.PreferredUsername ?? actor.Id?.Split('/').Last();
        var account = ApiMapper.ToAccount(actor);
        if (account == null)
            return NotFound(new { error = "Account not found." });

        account.FollowersCount = await _repository.GetFollowerCountAsync(username);
        account.FollowingCount = await _repository.GetFollowingCountAsync(username);
        account.StatusesCount = await _repository.GetNoteCountAsync(username);

        return Ok(account);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("accounts/{id}/statuses")]
    [ResponseCache(Duration = 3, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Statuses(string id, [FromQuery] int limit = 20)
    {
        var actor = await ResolveActorAsync(id);
        if (actor == null)
            return NotFound(new { error = "Account not found." });

        var username = actor.PreferredUsername ?? actor.Id?.Split('/').Last();
        limit = Math.Clamp(limit, 1, 40);

        var ids = await _repository.GetActorOutboxActivitiesAsync(username, 0, limit);
        var statuses = new List<ApiStatus>();
        var viewer = User.Identity?.Name;
        foreach (var aid in ids)
        {
            var activity = await _repository.GetActivityAsync(aid);
            if (activity == null)
                continue;
            var status = await ApiMapper.ToStatusAsync(_repository, activity, viewer);
            if (status != null)
                statuses.Add(status);
        }

        return Ok(statuses);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("accounts/{id}/followers")]
    [ResponseCache(Duration = 3, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Followers(string id, [FromQuery] int limit = 20)
    {
        var actor = await ResolveActorAsync(id);
        if (actor == null)
            return NotFound(new { error = "Account not found." });

        var username = actor.PreferredUsername ?? actor.Id?.Split('/').Last();
        limit = Math.Clamp(limit, 1, 40);

        var followerIds = await _repository.GetFollowersAsync(username, 0, limit);
        var accounts = new List<ApiAccount>();
        foreach (var fid in followerIds)
        {
            var follower = await ResolveActorAsync(fid);
            if (follower != null)
            {
                var acc = ApiMapper.ToAccount(follower);
                if (acc != null)
                    accounts.Add(acc);
            }
        }

        return Ok(accounts);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("accounts/{id}/following")]
    [ResponseCache(Duration = 3, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Following(string id, [FromQuery] int limit = 20)
    {
        var actor = await ResolveActorAsync(id);
        if (actor == null)
            return NotFound(new { error = "Account not found." });

        var username = actor.PreferredUsername ?? actor.Id?.Split('/').Last();
        limit = Math.Clamp(limit, 1, 40);

        var followingIds = await _repository.GetFollowingAsync(username, 0, limit);
        var accounts = new List<ApiAccount>();
        foreach (var fuid in followingIds)
        {
            var following = await ResolveActorAsync(fuid);
            if (following != null)
            {
                var acc = ApiMapper.ToAccount(following);
                if (acc != null)
                    accounts.Add(acc);
            }
        }

        return Ok(accounts);
    }

    /// <summary>
    /// Resolve an actor by preferred username (local) or by a remote actor URL.
    /// </summary>
    private async Task<ActivityPub.Core.Models.Actor?> ResolveActorAsync(string id)
    {
        if (id.Contains('/'))
        {
            var username = id.Split('/').Last();
            var byName = await _repository.GetUserActorAsync(username);
            if (byName != null)
                return byName;
        }
        return await _repository.GetUserActorAsync(id);
    }
}
