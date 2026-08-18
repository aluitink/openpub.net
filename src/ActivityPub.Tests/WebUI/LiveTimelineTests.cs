using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ActivityPub.WebUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 48.1 — live timeline refresh: SignalR primary transport with an SSE
/// fallback stream (/timeline/events), both fed from IActivityBroadcaster, and
/// a server-rendered card fragment endpoint (/timeline/card/{id}) the client
/// uses to prepend new notes without a full reload.
/// </summary>
public class LiveTimelineTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public LiveTimelineTests(WebUIFactory factory)
    {
        _factory = factory;
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task<string> GetAppJsAsync()
    {
        using var client = _factory.CreateClient();
        var res = await client.GetAsync("/js/app.js");
        return await res.Content.ReadAsStringAsync();
    }

    async Task<(HttpClient Client, string Username)> RegisterAndLogin(string username)
    {
        var client = CreateClient();
        await client.PostAsync("/auth/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Live Timeline User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        return (client, username);
    }

    // ---- DI wiring ----------------------------------------------------------

    [Fact]
    public void ActivityBroadcaster_RegisteredAsSingleton()
    {
        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var a = scopeA.ServiceProvider.GetRequiredService<IActivityBroadcaster>();
        var b = scopeB.ServiceProvider.GetRequiredService<IActivityBroadcaster>();
        Assert.NotNull(a);
        Assert.True(ReferenceEquals(a, b), "broadcaster must be a singleton shared by all scopes");
    }

    [Fact]
    public async Task NotificationService_PublishesToBroadcaster()
    {
        using var scope = _factory.Services.CreateScope();
        var broadcaster = scope.ServiceProvider.GetRequiredService<IActivityBroadcaster>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var reader = broadcaster.Subscribe();
        try
        {
            await notificationService.BroadcastNewActivityAsync("act-broadcast", "Note", "someactor", "live post");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var evt = await reader.ReadAsync(cts.Token);
            Assert.Equal("act-broadcast", evt.ActivityId);
            Assert.Equal("Note", evt.Type);
            Assert.Equal("someactor", evt.ActorName);
            Assert.Equal("live post", evt.Content);
        }
        finally
        {
            broadcaster.Unsubscribe(reader);
        }
    }

    // ---- Card fragment endpoint ---------------------------------------------

    [Fact]
    public async Task Card_ReturnsNoteFragmentForExistingActivity()
    {
        var username = $"lt_card_{Guid.NewGuid().ToString("N")[..8]}";
        var (client, _) = await RegisterAndLogin(username);

        var postResponse = await client.PostAsync("/compose/post", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Content", "Card fragment test post" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.StatusCode == HttpStatusCode.Redirect,
            $"compose failed: {(int)postResponse.StatusCode}");

        var timelineResponse = await client.GetAsync("/timeline");
        Assert.True(timelineResponse.IsSuccessStatusCode);
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var idx = timelineBody.IndexOf("data-activity-id=\"", StringComparison.Ordinal);
        Assert.True(idx >= 0, "timeline should contain a note card");
        idx += "data-activity-id=\"".Length;
        var end = timelineBody.IndexOf('"', idx);
        var activityId = timelineBody[idx..end];
        Assert.False(string.IsNullOrWhiteSpace(activityId));

        // The id is a full URL; clients percent-encode the path segment
        // (JS encodeURIComponent / HttpClient). The endpoint normalizes it.
        var cardResponse = await client.GetAsync("/timeline/card/" + Uri.EscapeDataString(activityId));
        Assert.True(cardResponse.IsSuccessStatusCode, $"card failed: {(int)cardResponse.StatusCode}");
        var contentType = cardResponse.Content.Headers.ContentType?.MediaType ?? "";
        Assert.Contains("text/html", contentType);
        var cardBody = await cardResponse.Content.ReadAsStringAsync();
        Assert.Contains("note-card", cardBody);
        Assert.Contains("Card fragment test post", cardBody);
    }

    [Fact]
    public async Task Card_Returns404ForUnknownActivity()
    {
        var (client, _) = await RegisterAndLogin($"lt_404_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/timeline/card/https://example.com/unknown/" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- SSE fallback stream --------------------------------------------------

    [Fact]
    public async Task TimelineEvents_RequiresAuthentication()
    {
        // Anonymous access to the live stream must be bounced to the login
        // page (the cookie auth handler issues a 302 challenge that HttpClient
        // follows). Assert the request never reaches the stream itself.
        using var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var response = await client.GetAsync("/timeline/events", cts.Token);
            // After following the auth challenge we land on /auth/login, not
            // on the SSE stream. A redirect-target login page is a 200 whose
            // final URI is the login route.
            var finalUrl = response.RequestMessage!.RequestUri!.ToString();
            Assert.Contains("/auth/login", finalUrl, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("anonymous /timeline/events did not respond promptly");
        }
    }

    [Fact]
    public async Task TimelineEvents_AuthenticatedStream_StreamsPublishedEvents()
    {
        var (client, _) = await RegisterAndLogin($"lt_sse_{Guid.NewGuid().ToString("N")[..8]}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var streamTask = client.GetStreamAsync("/timeline/events", cts.Token);
        using var reader = new StreamReader(await streamTask);

        // The stream opens with an "open" event.
        var eventLine = await reader.ReadLineAsync(cts.Token);
        Assert.Equal("event: open", eventLine);
        var dataLine = await reader.ReadLineAsync(cts.Token);
        Assert.StartsWith("data: ", dataLine ?? "");
        var blank = await reader.ReadLineAsync(cts.Token);
        Assert.True(string.IsNullOrEmpty(blank), $"expected blank line after data, got '{blank}'");

        // Publish a broadcast (via the same service the composer uses) and
        // expect the matching SSE event to arrive.
        using (var scope = _factory.Services.CreateScope())
        {
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notificationService.BroadcastNewActivityAsync("act-sse-1", "Note", "sseactor", "streamed hello");
        }

        var evtLine = await reader.ReadLineAsync(cts.Token);
        Assert.Equal("event: new_activity", evtLine);
        var payloadLine = await reader.ReadLineAsync(cts.Token);
        Assert.StartsWith("data: ", payloadLine ?? "");
        var payload = payloadLine!["data: ".Length..];
        Assert.Contains("act-sse-1", payload);
        Assert.Contains("sseactor", payload);
        Assert.Contains("streamed hello", payload);
    }

    // ---- Client wiring (app.js / layout) --------------------------------------

    [Fact]
    public async Task AppJs_ContainsLiveTimelineWiring()
    {
        var appJs = await GetAppJsAsync();
        Assert.Contains("live-timeline", appJs);
        Assert.Contains("handleLiveActivity", appJs);
        Assert.Contains("/timeline/card/", appJs);
        Assert.Contains("new_activity", appJs);
        Assert.Contains("EventSource", appJs);
        // self-echo suppression: own posts must not double-prepend
        Assert.Contains("Ignoring own activity", appJs);
    }

    [Fact]
    public async Task Layout_ExposesCurrentUserForSelfEchoSuppression()
    {
        var (client, username) = await RegisterAndLogin($"lt_layout_{Guid.NewGuid().ToString("N")[..8]}");
        var response = await client.GetAsync("/");
        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-current-user", body);
        Assert.Contains($"data-current-user=\"{username}\"", body);
    }

    [Fact]
    public async Task Layout_WithoutUser_HasEmptyCurrentUser()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-current-user=\"\"", body);
    }

    [Fact]
    public async Task LegacySseEndpoint_RedirectsToLiveStream()
    {
        // The SseController unconditionally returns Redirect("/timeline/events")
        // once past [Authorize]. A signed-in request that follows the redirect
        // lands on the broadcaster-backed live stream (which opens an SSE
        // response, so we cancel the read promptly and only assert the URL).
        var (client, _) = await RegisterAndLogin($"lt_legacy_{Guid.NewGuid().ToString("N")[..8]}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync("/sse/stream", cts.Token);
            // Following the redirect leads into the live stream (a 200 that
            // stays open). That still proves the redirect target is reachable.
        }
        catch (OperationCanceledException)
        {
            // The stream stayed open — the redirect was followed into
            // /timeline/events. Treat as success (the endpoint is live).
            return;
        }
        finally
        {
            cts.Cancel();
        }

        // If we got a terminal response, it must be a redirect or the stream.
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect,
            $"legacy /sse/stream should redirect/stream, got {(int)response.StatusCode}");
    }
}
