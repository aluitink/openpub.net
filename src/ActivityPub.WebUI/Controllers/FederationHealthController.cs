using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class FederationHealthController : Controller
{
    private readonly IFederationHealthService _healthService;
    private readonly ILogger<FederationHealthController> _logger;

    public FederationHealthController(
        IFederationHealthService healthService,
        ILogger<FederationHealthController> logger)
    {
        _healthService = healthService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        return View(await BuildViewModelAsync());
    }

    [HttpPost]
    public async Task<IActionResult> ProbeServers([FromForm] ICollection<string> domains, string? newDomain)
    {
        var allDomains = (domains ?? new List<string>()).ToList();

        if (!string.IsNullOrWhiteSpace(newDomain))
        {
            allDomains.Add(newDomain.Trim());
        }

        FederationHealthViewModel model;

        if (!allDomains.Any())
        {
            model = await BuildViewModelAsync();
            return View("Index", model);
        }

        var serverIds = allDomains.Select(d => "https://" + d).ToList();
        var results = await _healthService.ProbeRemoteServersAsync(serverIds);
        model = await BuildViewModelAsync();
        model.DomainsToProbe = allDomains;
        model.ProbeResults = results;

        return View("Index", model);
    }

    public async Task<IActionResult> ApiStatus()
    {
        var health = await _healthService.GetHealthStatusAsync();
        return Json(health);
    }

    public async Task<IActionResult> ApiErrors(int limit = 50)
    {
        var errs = await _healthService.GetRecentErrorsAsync(limit);
        return Json(errs);
    }

    private async Task<FederationHealthViewModel> BuildViewModelAsync()
    {
        var st = await _healthService.GetHealthStatusAsync();
        var errs = await _healthService.GetRecentErrorsAsync(20);

        return new FederationHealthViewModel
        {
            OverallStatus = st.OverallStatus,
            LastChecked = st.LastChecked,
            DeliveryQueue = st.DeliveryQueue,
            ActivityProcessing = st.ActivityProcessing,
            Database = st.Database,
            RecentErrors = errs
        };
    }
}

public class FederationHealthViewModel
{
    public string OverallStatus { get; set; } = "Healthy";
    public DateTime LastChecked { get; set; }
    public DeliveryQueueStats? DeliveryQueue { get; set; }
    public ActivityProcessingStats? ActivityProcessing { get; set; }
    public DatabaseStats? Database { get; set; }
    public ICollection<FederationErrorLog> RecentErrors { get; set; } = new List<FederationErrorLog>();
    public ICollection<string> DomainsToProbe { get; set; } = new List<string>();
    public ICollection<RemoteServerProbeResult>? ProbeResults { get; set; }
}
