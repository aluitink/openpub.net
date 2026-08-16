using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Obj = ActivityPub.Core.Models.Object;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
[Route("suggestions")]
[Route("discover")]
public class SuggestionsController : Controller
{
    private readonly IDiscoveryService _discovery;
    private readonly IActivityPubRepository _repository;
    private readonly ApplicationDbContext _identityDb;

    public SuggestionsController(
        IDiscoveryService discovery,
        IActivityPubRepository repository,
        ApplicationDbContext identityDb)
    {
        _discovery = discovery;
        _repository = repository;
        _identityDb = identityDb;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        if (user == null || string.IsNullOrEmpty(user.ActorId))
            return NotFound();

        var suggestions = await _discovery.GetFollowerSuggestionsAsync(user.ActorId);

        var suggestionItems = new List<SuggestionItem>();

        foreach (var actorId in suggestions)
        {
            var username = GetUsernameFromActorId(actorId);
            var actor = await _repository.GetUserActorAsync(username);
            if (actor != null)
            {
                suggestionItems.Add(new SuggestionItem
                {
                    ActorId = actor.Id ?? "",
                    Username = GetUsernameFromActorId(actor.Id ?? ""),
                    DisplayName = actor.Name ?? "",
                    Bio = actor.Summary ?? "",
                    AvatarUrl = actor.Icon?.ToString() ?? "",
                    Followers = await _repository.GetFollowerCountAsync(actor.Id ?? "")
                });
            }
        }

        return View(new SuggestionsViewModel
        {
            Suggestions = suggestionItems,
            MutedUsers = await _discovery.GetMutedUsersAsync(user.ActorId),
            ContentFilters = await _discovery.GetContentFiltersAsync(user.ActorId)
        });
    }

    [HttpPost]
    [Route("mute/{userId}")]
    public async Task<IActionResult> MuteUser(string userId)
    {
        var user = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        if (user == null) return Unauthorized();

        await _discovery.AddMutedUserAsync(user.ActorId!, userId);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Route("unmute/{userId}")]
    public async Task<IActionResult> UnmuteUser(string userId)
    {
        var user = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        if (user == null) return Unauthorized();

        await _discovery.RemoveMutedUserAsync(user.ActorId!, userId);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Route("add-filter")]
    public async Task<IActionResult> AddFilter([FromBody] FilterRequest req)
    {
        var user = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        if (user == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Keyword))
            return BadRequest("Keyword is required");

        await _discovery.AddContentFilterAsync(user.ActorId!, req.Keyword!);
        return Ok();
    }

    [HttpPost]
    [Route("remove-filter")]
    public async Task<IActionResult> RemoveFilter([FromBody] FilterRequest req)
    {
        var user = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        if (user == null) return Unauthorized();

        await _discovery.RemoveContentFilterAsync(user.ActorId!, req.Keyword!);
        return Ok();
    }

    private static string GetUsernameFromActorId(string actorId)
    {
        var lastSlash = actorId.LastIndexOf('/');
        return lastSlash >= 0 ? actorId.Substring(lastSlash + 1) : actorId;
    }
}

public class SuggestionsViewModel
{
    public List<SuggestionItem> Suggestions { get; set; } = new();
    public ICollection<string> MutedUsers { get; set; } = new List<string>();
    public ICollection<string> ContentFilters { get; set; } = new List<string>();
}

public class SuggestionItem
{
    public string ActorId { get; set; } = "";
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public int Followers { get; set; }
}

public class FilterRequest
{
    public string? Keyword { get; set; }
}
