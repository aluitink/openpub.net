using System.Security.Claims;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
[Route("communities")]
public class CommunitiesController : Controller
{
    private readonly ICommunityService _communityService;
    private readonly ApplicationDbContext _identityDb;
    private readonly ActivityPubOptions _options;

    public CommunitiesController(
        ICommunityService communityService,
        ApplicationDbContext identityDb,
        IOptions<ActivityPubOptions> options)
    {
        _communityService = communityService;
        _identityDb = identityDb;
        _options = options.Value;
    }

    [HttpGet]
    [Route("")]
    public async Task<IActionResult> Index()
    {
        var communities = await _communityService.GetAllCommunitiesAsync();
        var currentUser = await GetCurrentActorId();

        var model = new CommunitiesIndexViewModel
        {
            Communities = await Task.WhenAll(communities.Select(async c => new CommunityViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Summary = c.Summary,
                MemberCount = await _communityService.GetMemberCountAsync(c.Id),
                IsMember = currentUser != null && await _communityService.IsMemberAsync(currentUser, c.Id),
                IsOwner = currentUser != null && c.OwnerId == currentUser
            })),
            CurrentUserId = currentUser
        };

        return View(model);
    }

    [HttpGet]
    [Route("create")]
    public IActionResult Create()
    {
        return View(new CreateCommunityViewModel());
    }

    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> Create([FromForm] CreateCommunityViewModel model)
    {
        var currentUser = await GetCurrentActorId();
        if (currentUser == null) return RedirectToAction("Index");

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError("Name", "Name is required");
            return View(model);
        }

        var community = await _communityService.CreateCommunityAsync(currentUser, model.Name.Trim(), model.Summary?.Trim());

        if (community == null)
        {
            ModelState.AddModelError("", "Failed to create community. Check that you have an actor profile.");
            return View(model);
        }

        return RedirectToAction("Show", new { communityId = community.Id });
    }

    [HttpGet]
    [Route("{communityId}")]
    public async Task<IActionResult> Show(string communityId)
    {
        if (communityId is "create" or "my" or "search")
            return RedirectToAction(communityId == "create" ? "Create" : communityId == "my" ? "MyCommunities" : "Search");

        var community = await _communityService.GetCommunityByIdAsync(communityId);
        if (community == null) return NotFound();

        var currentUser = await GetCurrentActorId();

        var model = new CommunityShowViewModel
        {
            Community = community,
            MemberCount = await _communityService.GetMemberCountAsync(communityId),
            MemberIds = await _communityService.GetMemberIdsAsync(communityId),
            IsMember = currentUser != null && await _communityService.IsMemberAsync(currentUser, communityId),
            IsOwner = currentUser != null && community.OwnerId == currentUser,
            CurrentUserId = currentUser
        };

        return View(model);
    }

    [HttpPost]
    [Route("{communityId}/join")]
    public async Task<IActionResult> Join(string communityId)
    {
        var currentUser = await GetCurrentActorId();
        if (currentUser == null) return RedirectToAction("Index");

        await _communityService.JoinCommunityAsync(currentUser, communityId);
        return RedirectToAction("Show", new { communityId });
    }

    [HttpPost]
    [Route("{communityId}/leave")]
    public async Task<IActionResult> Leave(string communityId)
    {
        var currentUser = await GetCurrentActorId();
        if (currentUser == null) return RedirectToAction("Index");

        await _communityService.LeaveCommunityAsync(currentUser, communityId);
        return RedirectToAction("Show", new { communityId });
    }

    [HttpPost]
    [Route("{communityId}/delete")]
    public async Task<IActionResult> Delete(string communityId)
    {
        var currentUser = await GetCurrentActorId();
        if (currentUser == null) return RedirectToAction("Index");

        var result = await _communityService.DeleteCommunityAsync(currentUser, communityId);
        return RedirectToAction("Index");
    }

    [HttpGet]
    [Route("my")]
    public async Task<IActionResult> MyCommunities()
    {
        var currentUser = await GetCurrentActorId();
        if (currentUser == null) return RedirectToAction("Index");

        var communities = await _communityService.GetMyCommunitiesAsync(currentUser);

        var model = new MyCommunitiesViewModel
        {
            Communities = communities.Select(c => new CommunityViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Summary = c.Summary,
                IsMember = true,
                IsOwner = c.OwnerId == currentUser
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    [Route("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return RedirectToAction("Index");

        var results = await _communityService.SearchCommunitiesAsync(q);

        var model = new SearchCommunitiesViewModel
        {
            Query = q,
            Communities = results.Select(c => new CommunityViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Summary = c.Summary
            }).ToList()
        };

        return View(model);
    }

    private async Task<string?> GetCurrentActorId()
    {
        var username = User.Identity?.Name;
        if (username == null) return null;

        var user = await _identityDb.Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == username.ToUpperInvariant());

        return user?.ActorId;
    }

    private static string GetUsernameFromId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return "Unknown";
        var lastSlash = id.LastIndexOf('/');
        return lastSlash >= 0 ? id.Substring(lastSlash + 1) : id;
    }
}

public class CommunityViewModel
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Summary { get; set; }
    public int MemberCount { get; set; }
    public bool IsMember { get; set; }
    public bool IsOwner { get; set; }
}

public class CommunitiesIndexViewModel
{
    public ICollection<CommunityViewModel> Communities { get; set; } = new List<CommunityViewModel>();
    public string? CurrentUserId { get; set; }
}

public class CommunityShowViewModel
{
    public required ActivityPub.Core.Models.Community Community { get; set; }
    public int MemberCount { get; set; }
    public ICollection<string> MemberIds { get; set; } = new List<string>();
    public bool IsMember { get; set; }
    public bool IsOwner { get; set; }
    public string? CurrentUserId { get; set; }
}

public class CreateCommunityViewModel
{
    public string? Name { get; set; }
    public string? Summary { get; set; }
}

public class MyCommunitiesViewModel
{
    public ICollection<CommunityViewModel> Communities { get; set; } = new List<CommunityViewModel>();
}

public class SearchCommunitiesViewModel
{
    public required string Query { get; set; }
    public ICollection<CommunityViewModel> Communities { get; set; } = new List<CommunityViewModel>();
}
