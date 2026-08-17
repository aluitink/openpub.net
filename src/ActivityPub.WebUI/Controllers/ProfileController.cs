using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ApplicationDbContext _identityDb;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IActivityPubRepository repository, ApplicationDbContext identityDb, ILogger<ProfileController> logger)
    {
        _repository = repository;
        _identityDb = identityDb;
        _logger = logger;
    }

    [HttpGet]
    [Route("Profile")]
    [ResponseCache(Duration = 5, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Index(string? username = null, string? returnUrl = null)
    {
        var targetUsername = username ?? User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(targetUsername);
        if (actor == null)
        {
            return NotFound();
        }

        var followingCount = await _repository.GetFollowingCountAsync(targetUsername);
        var followerCount = await _repository.GetFollowerCountAsync(targetUsername);
        var noteCount = await _repository.GetNoteCountAsync(targetUsername);

        var currentUsername = User.Identity!.Name!;
        var isOwnProfile = targetUsername == currentUsername;
        var isFollowing = !isOwnProfile && await _repository.IsFollowingAsync(currentUsername, actor.Id ?? string.Empty);

        var viewModel = new ProfileViewModel
        {
            Username = targetUsername,
            DisplayName = actor.Name ?? targetUsername,
            Bio = actor.Summary ?? "",
            IconUrl = actor.Icon?.Url ?? "",
            BannerUrl = actor.Image?.Url ?? "",
            NoteCount = noteCount,
            FollowingCount = followingCount,
            FollowerCount = followerCount,
            IsOwnProfile = isOwnProfile,
            IsFollowing = isFollowing,
            TargetActorId = actor.Id,
            ReturnUrl = returnUrl ?? (isOwnProfile ? null : $"/Profile?username={targetUsername}"),
            JoinedDate = actor.Published?.ToString("MMMM yyyy") ?? "Recently"
        };

        return View(viewModel);
    }

    [HttpPost]
    [Route("Profile/Follow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Follow(string? actorId, string? returnUrl = null)
    {
        var username = User.Identity!.Name!;
        var localActor = await _repository.GetUserActorAsync(username);
        if (localActor == null || string.IsNullOrEmpty(actorId))
        {
            return RedirectToAction("Index", "Profile");
        }

        if (actorId == localActor.Id)
        {
            TempData["ProfileError"] = "You cannot follow yourself.";
            return Redirect(ResolveProfileUrl(returnUrl));
        }

        var already = await _repository.IsFollowingAsync(username, actorId);
        if (already)
        {
            return Redirect(ResolveProfileUrl(returnUrl));
        }

        var now = DateTime.UtcNow;
        var followId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";
        var followActivity = new Follow
        {
            Id = followId,
            Type = "Follow",
            Actor = localActor.Id,
            Object = actorId,
            Published = now,
            To = new List<string> { actorId }
        };
        await _repository.SaveActivityAsync(followActivity);

        _logger.LogInformation("User {Username} followed {TargetActorId} from profile page", username, actorId);
        TempData["FollowSuccess"] = true;
        return Redirect(ResolveProfileUrl(returnUrl));
    }

    [HttpPost]
    [Route("Profile/Unfollow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unfollow(string? actorId, string? returnUrl = null)
    {
        var username = User.Identity!.Name!;
        var localActor = await _repository.GetUserActorAsync(username);
        if (localActor == null || string.IsNullOrEmpty(actorId))
        {
            return Redirect(ResolveProfileUrl(returnUrl));
        }

        if (!await _repository.IsFollowingAsync(username, actorId))
        {
            return Redirect(ResolveProfileUrl(returnUrl));
        }

        // Find the active Follow activity authored by this user toward the target.
        string? followActivityId = null;
        var outboxActivities = await _repository.GetActorOutboxActivitiesAsync(username, 0, 200);
        foreach (var outboxId in outboxActivities)
        {
            var activity = await _repository.GetActivityAsync(outboxId);
            if (activity?.Type == "Follow" && activity.ObjectId == actorId)
            {
                followActivityId = outboxId;
                break;
            }
        }

        if (followActivityId != null)
        {
            var now = DateTime.UtcNow;
            var undoId = $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";
            var undoActivity = new Activity
            {
                Id = undoId,
                Type = "Undo",
                Actor = localActor.Id,
                Object = new Activity
                {
                    Id = followActivityId,
                    Type = "Follow",
                    Actor = localActor.Id,
                    Object = actorId,
                    Published = now
                },
                Published = now,
                To = new List<string> { actorId }
            };
            await _repository.SaveActivityAsync(undoActivity);
            await _repository.DeleteActivityAsync(followActivityId);
            _logger.LogInformation("User {Username} unfollowed {TargetActorId} from profile page", username, actorId);
        }

        TempData["UnfollowSuccess"] = true;
        return Redirect(ResolveProfileUrl(returnUrl));
    }

    /// <summary>
    /// Resolves the profile URL to return to after a follow/unfollow action.
    /// Falls back to the viewer's own profile when no valid return URL is provided.
    /// </summary>
    private string ResolveProfileUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl) &&
            returnUrl.StartsWith("/Profile", StringComparison.OrdinalIgnoreCase))
        {
            return returnUrl;
        }
        return Url.Action("Index", "Profile")!;
    }

    [HttpGet]
    [Route("Profile/Edit")]
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
    [Route("Profile/Edit")]
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

        // Keep the identity record in sync so Search shows the avatar/banner.
        var identityUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (identityUser != null)
        {
            identityUser.DisplayName = model.DisplayName.Trim();
            identityUser.Bio = string.IsNullOrWhiteSpace(model.Bio) ? "" : model.Bio.Trim();
            identityUser.AvatarUrl = actor.Icon?.Url ?? "";
            identityUser.BannerUrl = actor.Image?.Url;
            await _identityDb.SaveChangesAsync();
        }

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
    public int NoteCount { get; set; }
    public int FollowingCount { get; set; }
    public int FollowerCount { get; set; }
    public bool IsOwnProfile { get; set; }
    public bool IsFollowing { get; set; }
    public string? TargetActorId { get; set; }
    public string? ReturnUrl { get; set; }
    public string JoinedDate { get; set; } = string.Empty;
}

public class EditProfileModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? IconUrl { get; set; }
    public string? BannerUrl { get; set; }
}
