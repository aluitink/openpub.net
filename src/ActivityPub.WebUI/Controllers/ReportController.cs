using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityPub.WebUI.Services;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly IUserReportService _reportService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(IUserReportService reportService, ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Form(string? targetUsername, string? activityId)
    {
        ViewData["TargetUsername"] = targetUsername;
        ViewData["ActivityId"] = activityId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string targetUsername, string reason, string? activityId)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        if (string.IsNullOrEmpty(targetUsername) || string.IsNullOrEmpty(reason))
            return BadRequest("Target username and reason are required");

        if (username == targetUsername)
            return BadRequest("You cannot report yourself");

        await _reportService.SubmitReportAsync(username, targetUsername, reason, activityId);
        TempData["Success"] = "Report submitted successfully";
        return RedirectToAction("Index", "Timeline");
    }
}
