using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers;

/// <summary>
/// Legacy SSE endpoint. Phase 48 replaced it with <c>/timeline/events</c>
/// (broadcaster-backed stream with periodic heartbeats); the old
/// implementation polled the entire Activities table every 3 seconds and
/// only ever emitted the newest activity at connect time. The route is kept
/// as a redirect so older clients and bookmarks land on the live stream.
/// </summary>
[Authorize]
public class SseController : Controller
{
    [HttpGet]
    public IActionResult Stream()
    {
        return Redirect("/timeline/events");
    }
}
