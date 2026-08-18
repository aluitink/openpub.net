using ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(IActivityPubRepository repository, ILogger<SettingsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var username = User.Identity!.Name!;
        var actor = await _repository.GetUserActorAsync(username);
        if (actor == null)
            return NotFound();

        var blurPref = await _repository.GetBlurSensitiveMediaAsync(username);

        var model = new SettingsViewModel
        {
            // Default to true (blur sensitive media) when never explicitly set,
            // matching the Mastodon-style default of protecting the reader.
            BlurSensitiveMedia = blurPref ?? true
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDisplay([FromForm] bool blurSensitiveMedia)
    {
        var username = User.Identity!.Name!;
        await _repository.SetBlurSensitiveMediaAsync(username, blurSensitiveMedia);
        TempData["SettingsSuccess"] = "Settings saved.";
        return RedirectToAction(nameof(Index));
    }
}

public class SettingsViewModel
{
    public bool BlurSensitiveMedia { get; set; } = true;
}
