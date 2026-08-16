using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class RealtimeSettingsController : Controller
{
    [HttpGet]
    public IActionResult Get()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var settings = new
        {
            pollingIntervalMs = 30000,
            sseEnabled = true,
            signalrEnabled = true,
            desktopNotificationsEnabled = false,
            notificationSoundEnabled = true,
            maxConnections = 5
        };
        return Ok(settings);
    }

    [HttpPost]
    public IActionResult Update([FromBody] RealtimeSettingsRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var clampedInterval = Math.Clamp(request.PollingIntervalMs, 5000, 300000);

        var settings = new
        {
            pollingIntervalMs = clampedInterval,
            sseEnabled = request.SseEnabled,
            signalrEnabled = request.SignalREnabled,
            desktopNotificationsEnabled = request.DesktopNotificationsEnabled,
            notificationSoundEnabled = request.NotificationSoundEnabled,
            maxConnections = Math.Clamp(request.MaxConnections, 1, 10)
        };
        return Ok(settings);
    }
}

public class RealtimeSettingsRequest
{
    public int PollingIntervalMs { get; set; } = 30000;
    public bool SseEnabled { get; set; } = true;
    public bool SignalREnabled { get; set; } = true;
    public bool DesktopNotificationsEnabled { get; set; } = false;
    public bool NotificationSoundEnabled { get; set; } = true;
    public int MaxConnections { get; set; } = 5;
}
