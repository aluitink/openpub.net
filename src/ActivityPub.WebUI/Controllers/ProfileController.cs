using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IActivityPubRepository repository, ILogger<ProfileController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    [ResponseCache(Duration = 5, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Index(string? username = null)
    {
        var targetUsername = username ?? User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(targetUsername);
        if (actor == null)
        {
            return NotFound();
        }

        var followingCount = await _repository.GetFollowingCountAsync(targetUsername);
        var followerCount = await _repository.GetFollowerCountAsync(targetUsername);

        var isOwnProfile = targetUsername == User.Identity.Name;

        var viewModel = new ProfileViewModel
        {
            Username = targetUsername,
            DisplayName = actor.Name ?? targetUsername,
            Bio = actor.Summary ?? "",
            IconUrl = actor.Icon?.Url ?? "",
            BannerUrl = actor.Image?.Url ?? "",
            FollowingCount = followingCount,
            FollowerCount = followerCount,
            IsOwnProfile = isOwnProfile,
            JoinedDate = actor.Published?.ToString("MMMM yyyy") ?? "Recently"
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return NotFound();
        }

        var viewModel = new EditProfileModel
        {
            DisplayName = actor.Name ?? "",
            Bio = actor.Summary ?? "",
            IconUrl = actor.Icon?.Url ?? "",
            BannerUrl = actor.Image?.Url ?? ""
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileModel model)
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(model.DisplayName) || model.DisplayName.Length > 100)
        {
            ModelState.AddModelError("DisplayName", "Display name must be between 1 and 100 characters.");
        }

        if (model.Bio != null && model.Bio.Length > 500)
        {
            ModelState.AddModelError("Bio", "Bio must be 500 characters or less.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        actor.Name = model.DisplayName.Trim();
        actor.Summary = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();

        if (!string.IsNullOrEmpty(model.IconUrl))
        {
            actor.Icon = new Image { Url = model.IconUrl.Trim() };
        }
        else
        {
            actor.Icon = null;
        }

        if (!string.IsNullOrEmpty(model.BannerUrl))
        {
            actor.Image = new Image { Url = model.BannerUrl.Trim() };
        }
        else
        {
            actor.Image = null;
        }

        actor.Updated = DateTime.UtcNow;
        await _repository.SaveUserActorAsync(actor);

        _logger.LogInformation("User {Username} updated profile", username);
        TempData["ProfileUpdated"] = true;
        return RedirectToAction("Index", "Profile");
    }
}

public class ProfileViewModel
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string BannerUrl { get; set; } = string.Empty;
    public int FollowingCount { get; set; }
    public int FollowerCount { get; set; }
    public bool IsOwnProfile { get; set; }
    public string JoinedDate { get; set; } = string.Empty;
}

public class EditProfileModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string BannerUrl { get; set; } = string.Empty;
}
