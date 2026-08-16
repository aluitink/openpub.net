using ActivityPub.WebUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class PushController : Controller
{
    private readonly IPushNotificationService _pushService;

    public PushController(IPushNotificationService pushService)
    {
        _pushService = pushService;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] PushSubscriptionRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        await _pushService.RegisterSubscriptionAsync(username, request.Endpoint, request.P256dh, request.Auth);
        return Ok(new { registered = true, username });
    }

    [HttpPost]
    public async Task<IActionResult> Test()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        await _pushService.SendPushNotificationAsync(username, "Test Notification", "This is a test from Fediblog.");
        return Ok(new { sent = true });
    }
}

public class PushSubscriptionRequest
{
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
}
