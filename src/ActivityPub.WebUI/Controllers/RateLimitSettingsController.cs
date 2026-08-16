using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class RateLimitSettingsController : Controller
{
    private readonly ILogger<RateLimitSettingsController> _logger;

    private static readonly Dictionary<string, RateLimitConfig> Settings = new()
    {
        { "compose", new RateLimitConfig { WindowMinutes = 1, Limit = 20 } },
        { "follow", new RateLimitConfig { WindowMinutes = 1, Limit = 10 } },
        { "upload", new RateLimitConfig { WindowMinutes = 1, Limit = 10 } }
    };

    public RateLimitSettingsController(ILogger<RateLimitSettingsController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new RateLimitSettingsViewModel
        {
            Compose = Settings["compose"],
            Follow = Settings["follow"],
            Upload = Settings["upload"]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update(RateLimitSettingsViewModel model)
    {
        model.Compose.Limit = Clamp(model.Compose.Limit, 1, 100);
        model.Compose.WindowMinutes = Clamp(model.Compose.WindowMinutes, 1, 60);
        model.Follow.Limit = Clamp(model.Follow.Limit, 1, 50);
        model.Follow.WindowMinutes = Clamp(model.Follow.WindowMinutes, 1, 60);
        model.Upload.Limit = Clamp(model.Upload.Limit, 1, 50);
        model.Upload.WindowMinutes = Clamp(model.Upload.WindowMinutes, 1, 60);

        Settings["compose"] = model.Compose;
        Settings["follow"] = model.Follow;
        Settings["upload"] = model.Upload;

        _logger.LogInformation("Rate limit settings updated");
        TempData["Success"] = "Rate limit settings updated";
        return RedirectToAction("Index");
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}

public class RateLimitConfig
{
    public int Limit { get; set; }
    public int WindowMinutes { get; set; }
}

public class RateLimitSettingsViewModel
{
    public RateLimitConfig Compose { get; set; } = new();
    public RateLimitConfig Follow { get; set; } = new();
    public RateLimitConfig Upload { get; set; } = new();
}
