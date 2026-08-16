using ActivityPub.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class SseController : Controller
{
    private readonly ActivityPubDbContext _db;
    private readonly ILogger<SseController> _logger;

    public SseController(ActivityPubDbContext db, ILogger<SseController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [Produces("text/event-stream")]
    public async Task<ActionResult> Stream()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var lastActivityId = string.Empty;

        try
        {
            while (HttpContext.RequestAborted.IsCancellationRequested == false)
            {
                var activities = await _db.Activities
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync(HttpContext.RequestAborted);

                foreach (var activity in activities)
                {
                    if (activity.ActivityId == lastActivityId)
                        break;

                    if (activity.ActivityId == lastActivityId)
                        continue;

                    lastActivityId = activity.ActivityId;

                    if (activity.JsonData.Contains(username))
                    {
                        var evtType = ExtractType(activity.JsonData);
                        var eventData = "data: " + System.Text.Json.JsonSerializer.Serialize(new
                        {
                            id = activity.ActivityId,
                            type = evtType,
                            created = activity.CreatedAt.ToString("o")
                        }) + "\n\n";

                        await Response.WriteAsync(eventData, HttpContext.RequestAborted);
                        await Response.Body.FlushAsync(HttpContext.RequestAborted);
                    }
                }

                await Task.Delay(3000, HttpContext.RequestAborted);

                var heartbeat = ": heartbeat\n\n";
                await Response.WriteAsync(heartbeat, HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSE connection closed for {Username}", username);
        }

        return NoContent();
    }

    static string ExtractType(string json)
    {
        var idx = json.IndexOf("\"type\"");
        if (idx < 0) return "Unknown";
        var colon = json.IndexOf(':', idx);
        var q1 = json.IndexOf('"', colon + 1);
        var q2 = json.IndexOf('"', q1 + 1);
        if (q1 > 0 && q2 > q1)
            return json.Substring(q1 + 1, q2 - q1 - 1);
        return "Unknown";
    }
}
