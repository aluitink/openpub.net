using ActivityPub.WebUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers;

/// <summary>
/// Server-Sent Events fallback for live timeline updates. Serves clients whose
/// browser cannot maintain a SignalR/WebSocket connection. Each event carries
/// the same payload as the <c>NewActivity</c> SignalR event; the client renders
/// it via the same <c>/timeline/card/{id}</c> fragment endpoint.
/// </summary>
[Authorize]
public class TimelineEventsController : Controller
{
    private readonly IActivityBroadcaster _broadcaster;

    public TimelineEventsController(IActivityBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    [HttpGet("/timeline/events")]
    [Produces("text/event-stream")]
    public async Task StreamEvents()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.Append("X-Accel-Buffering", "no");

        var reader = _broadcaster.Subscribe();
        try
        {
            // Initial "hello" event so the client knows the stream is open.
            await WriteEventAsync("open", new { ok = true, ts = DateTime.UtcNow.ToString("O") });

            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                // Block until the next event or a heartbeat interval elapses.
                // WaitToReadAsync completes immediately when a publish arrives,
                // so a fresh activity is streamed to the client without waiting
                // out the full idle window.
                try
                {
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
                    readCts.CancelAfter(HeartbeatInterval);
                    var readable = await reader.WaitToReadAsync(readCts.Token);
                    if (!readable)
                        break;
                }
                catch (OperationCanceledException)
                {
                    // Either the request aborted or the heartbeat interval
                    // elapsed with nothing to read.
                }

                if (HttpContext.RequestAborted.IsCancellationRequested)
                    break;

                if (reader.TryRead(out var evt))
                {
                    await WriteEventAsync("new_activity", new
                    {
                        activityId = evt.ActivityId,
                        type = evt.Type,
                        actorName = evt.ActorName,
                        content = evt.Content,
                        timestamp = evt.Timestamp
                    });
                }
                else
                {
                    // Idle heartbeat so proxies/EventSource clients detect a
                    // dead connection.
                    await Response.WriteAsync(": heartbeat\n\n", HttpContext.RequestAborted);
                    await Response.Body.FlushAsync(HttpContext.RequestAborted);
                }
            }
        }
        finally
        {
            _broadcaster.Unsubscribe(reader);
        }
    }

    private async Task WriteEventAsync(string name, object payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        await Response.WriteAsync($"event: {name}\ndata: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}
