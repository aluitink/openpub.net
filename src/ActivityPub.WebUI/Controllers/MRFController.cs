using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ActivityPub.Core.Options;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class MRFController : Controller
{
    private readonly IOptions<ActivityPubOptions> _options;
    private readonly ILogger<MRFController> _logger;

    public MRFController(IOptions<ActivityPubOptions> options, ILogger<MRFController> logger)
    {
        _options = options;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var adminUser = HttpContext.User.Identity?.Name;
        if (adminUser == null) return Forbid();

        var model = new MRFViewModel
        {
            ProhibitedWords = _options.Value.MRFOptions?.ProhibitedWords?.ToList() ?? new List<string>(),
            BlockedDomains = _options.Value.MRFOptions?.BlockedDomains?.ToList() ?? new List<string>(),
            MaxContentLength = _options.Value.MRFOptions?.MaxContentLength ?? 5000
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update(MRFViewModel model)
    {
        _options.Value.MRFOptions ??= new MRFOptions();
        _options.Value.MRFOptions.ProhibitedWords = model.ProhibitedWords;
        _options.Value.MRFOptions.BlockedDomains = model.BlockedDomains;
        _options.Value.MRFOptions.MaxContentLength = model.MaxContentLength;

        _logger.LogInformation("MRF settings updated: {WordCount} words, {DomainCount} domains",
            model.ProhibitedWords.Count, model.BlockedDomains.Count);

        TempData["Success"] = "MRF settings updated";
        return RedirectToAction("Index");
    }
}

public class MRFViewModel
{
    public List<string> ProhibitedWords { get; set; } = new();
    public List<string> BlockedDomains { get; set; } = new();
    public int MaxContentLength { get; set; } = 5000;
    public string? NewWord { get; set; }
    public string? NewDomain { get; set; }
}
