using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using ActivityPub.WebUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _identityDb;
    private readonly ActivityPubDbContext _activityPubDb;
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<AdminController> _logger;
    private readonly IAuditLogService _auditLog;
    private readonly IUserReportService _reportService;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext identityDb,
        ActivityPubDbContext activityPubDb,
        IActivityPubRepository repository,
        ILogger<AdminController> logger,
        IAuditLogService auditLog,
        IUserReportService reportService)
    {
        _userManager = userManager;
        _identityDb = identityDb;
        _activityPubDb = activityPubDb;
        _repository = repository;
        _logger = logger;
        _auditLog = auditLog;
        _reportService = reportService;
    }

    public bool IsAdmin
    {
        get
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return false;
            return ViewData["IsAdmin"] is true || _identityDb.Users.Any(u => u.UserName == username && u.IsAdmin);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Auth");

        var currentUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (currentUser == null || !currentUser.IsAdmin)
        {
            return Forbid();
        }

        var viewModel = new AdminDashboardViewModel
        {
            TotalUsers = await _identityDb.Users.CountAsync(),
            TotalActivities = await _activityPubDb.Activities.CountAsync(),
            TotalActors = await _activityPubDb.Actors.CountAsync(),
            BlockedUsers = await _identityDb.Users.CountAsync(u => u.IsBlocked),
            RecentUsers = await _identityDb.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(10)
                .Select(u => new AdminUserItem
                {
                    Id = u.Id,
                    Username = u.UserName ?? "",
                    Email = u.Email ?? "",
                    DisplayName = u.DisplayName,
                    CreatedAt = u.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    IsAdmin = u.IsAdmin,
                    IsBlocked = u.IsBlocked
                })
                .ToListAsync(),
            PendingDeliveries = await _activityPubDb.SharedInboxDeliveries
                .CountAsync(d => d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Failed)
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var username = User.Identity?.Name;
        var currentUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (currentUser == null || !currentUser.IsAdmin) return Forbid();

        var users = await _identityDb.Users
            .OrderBy(u => u.UserName)
            .Select(u => new AdminUserItem
            {
                Id = u.Id,
                Username = u.UserName ?? "",
                Email = u.Email ?? "",
                DisplayName = u.DisplayName,
                CreatedAt = u.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                IsAdmin = u.IsAdmin,
                IsBlocked = u.IsBlocked
            })
            .ToListAsync();

        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAdmin(string userId, bool isAdmin)
    {
        var adminUsername = User.Identity?.Name;
        var adminUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == adminUsername);
        if (adminUser == null || !adminUser.IsAdmin) return Forbid();

        var target = await _identityDb.Users.FindAsync(userId);
        if (target == null) return NotFound();

        if (target.Id == adminUser.Id && !isAdmin)
        {
            TempData["Error"] = "You cannot remove admin status from yourself.";
            return RedirectToAction("Users");
        }

        target.IsAdmin = isAdmin;
        await _identityDb.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminUser} set {TargetUser} admin={IsAdmin}", adminUsername, userId, isAdmin);
        await _auditLog.LogActionAsync(adminUsername!, "SetAdmin", userId, $"Set admin={isAdmin}");
        return RedirectToAction("Users");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlock(string userId)
    {
        var adminUsername = User.Identity?.Name;
        var adminUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == adminUsername);
        if (adminUser == null || !adminUser.IsAdmin) return Forbid();

        var target = await _identityDb.Users.FindAsync(userId);
        if (target == null) return NotFound();

        if (target.Id == adminUser.Id)
        {
            TempData["Error"] = "You cannot block yourself.";
            return RedirectToAction("Users");
        }

        target.IsBlocked = !target.IsBlocked;
        await _identityDb.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminUser} toggled block on {TargetUser} (now blocked={Blocked})", adminUsername, userId, target.IsBlocked);
        await _auditLog.LogActionAsync(adminUsername!, "ToggleBlock", userId, $"Blocked={target.IsBlocked}");
        return RedirectToAction("Users");
    }

    [HttpGet]
    public async Task<IActionResult> Moderation()
    {
        var username = User.Identity?.Name;
        var currentUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (currentUser == null || !currentUser.IsAdmin) return Forbid();

        var activities = await _activityPubDb.Activities
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new AdminActivityItem
            {
                Id = a.Id,
                ActivityId = a.ActivityId,
                CreatedAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                Type = ExtractTypeFromJson(a.JsonData),
                Actor = ExtractActorFromJson(a.JsonData)
            })
            .ToListAsync();

        var blockedDomains = new List<string>();

        return View(new AdminModerationViewModel
        {
            Activities = activities,
            BlockedDomains = blockedDomains,
            PendingDeliveries = await _activityPubDb.SharedInboxDeliveries
                .Where(d => d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Failed)
                .OrderBy(d => d.CreatedAt)
                .Take(20)
                .Select(d => new AdminDeliveryItem
                {
                    ActivityId = d.ActivityId,
                    TargetActorId = d.TargetActorId,
                    Status = d.Status.ToString(),
                    RetryCount = d.RetryCount,
                    CreatedAt = d.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteActivity(string activityId)
    {
        var adminUsername = User.Identity?.Name;
        var adminUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == adminUsername);
        if (adminUser == null || !adminUser.IsAdmin) return Forbid();

        await _repository.DeleteActivityAsync(activityId);
        _logger.LogInformation("Admin {AdminUser} deleted activity {ActivityId}", adminUsername, activityId);
        await _auditLog.LogActionAsync(adminUsername!, "DeleteActivity", activityId, "Activity deleted by admin");
        return RedirectToAction("Moderation");
    }

    [HttpGet]
    public async Task<IActionResult> Reports()
    {
        var username = User.Identity?.Name;
        var currentUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (currentUser == null || !currentUser.IsAdmin) return Forbid();

        var reports = await _reportService.GetPendingReportsAsync();
        return View(reports);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissReport(int reportId)
    {
        var adminUsername = User.Identity?.Name;
        var adminUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == adminUsername);
        if (adminUser == null || !adminUser.IsAdmin) return Forbid();

        await _reportService.DismissReportAsync(reportId, adminUsername!, "Dismissed by admin");
        await _auditLog.LogActionAsync(adminUsername!, "DismissReport", reportId.ToString(), "Report dismissed");
        return RedirectToAction("Reports");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TakeActionReport(int reportId)
    {
        var adminUsername = User.Identity?.Name;
        var adminUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == adminUsername);
        if (adminUser == null || !adminUser.IsAdmin) return Forbid();

        await _reportService.DeleteReportTargetAsync(reportId, adminUsername!);
        await _auditLog.LogActionAsync(adminUsername!, "TakeActionReport", reportId.ToString(), "Action taken on report");
        return RedirectToAction("Reports");
    }

    [HttpGet]
    public async Task<IActionResult> AuditLog()
    {
        var username = User.Identity?.Name;
        var currentUser = await _identityDb.Users.FirstOrDefaultAsync(u => u.UserName == username);
        if (currentUser == null || !currentUser.IsAdmin) return Forbid();

        var entries = await _auditLog.GetRecentEntriesAsync(100);
        return View(entries);
    }

    static string ExtractTypeFromJson(string json)
    {
        var idx = json.IndexOf("\"type\"");
        if (idx < 0) return "unknown";
        var colon = json.IndexOf(':', idx);
        var q1 = json.IndexOf('"', colon + 1);
        var q2 = json.IndexOf('"', q1 + 1);
        if (q1 > 0 && q2 > q1) return json.Substring(q1 + 1, q2 - q1 - 1);
        return "unknown";
    }

    static string ExtractActorFromJson(string json)
    {
        var idx = json.IndexOf("\"actor\"");
        if (idx < 0) return "unknown";
        var colon = json.IndexOf(':', idx);
        var q1 = json.IndexOf('"', colon + 1);
        var q2 = json.IndexOf('"', q1 + 1);
        if (q1 > 0 && q2 > q1)
        {
            var full = json.Substring(q1 + 1, q2 - q1 - 1);
            return full.Split('/').Last();
        }
        return "unknown";
    }
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalActivities { get; set; }
    public int TotalActors { get; set; }
    public int BlockedUsers { get; set; }
    public int PendingDeliveries { get; set; }
    public List<AdminUserItem> RecentUsers { get; set; } = new();
}

public class AdminUserItem
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsBlocked { get; set; }
}

public class AdminModerationViewModel
{
    public List<AdminActivityItem> Activities { get; set; } = new();
    public List<string> BlockedDomains { get; set; } = new();
    public List<AdminDeliveryItem> PendingDeliveries { get; set; } = new();
}

public class AdminActivityItem
{
    public int Id { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
}

public class AdminDeliveryItem
{
    public string ActivityId { get; set; } = string.Empty;
    public string TargetActorId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
