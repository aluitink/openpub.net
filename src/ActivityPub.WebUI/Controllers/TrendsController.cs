using ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers;

[Route("trends")]
[Route("trending")]
public class TrendsController : Controller
{
    private readonly IDiscoveryService _discovery;

    public TrendsController(IDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? period = null)
    {
        TimeSpan? window = period switch
        {
            "hourly" => TimeSpan.FromHours(1),
            "daily" => TimeSpan.FromDays(1),
            "weekly" => TimeSpan.FromDays(7),
            _ => null
        };

        var hashtags = await _discovery.GetTrendingHashtagsAsync(window, 20);

        return View(new TrendsViewModel
        {
            Hashtags = hashtags.ToList(),
            Period = period ?? "all"
        });
    }
}

public class TrendsViewModel
{
    public List<Core.Interfaces.TrendingHashtag> Hashtags { get; set; } = new();
    public string Period { get; set; } = "all";
}
