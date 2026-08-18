using System.Net;
using System.Text;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Unit tests for the *outbound* WebFinger resolver (WebFingerService). These
/// exercise the real code path against a stub HttpMessageHandler — no live
/// network — to prove the resolver builds the correct URL, sends the
/// ActivityPub Accept header, prefers a typed self link, and falls back to a
/// bare rel==self link (which many ActivityPub stacks emit without a type).
/// </summary>
public class WebFingerServiceTests
{
    /// <summary>
    /// Records every outgoing request and returns canned responses keyed by a
    /// URL match predicate.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public record Captured(HttpMethod Method, Uri Uri, string? Accept);
        public List<Captured> Requests { get; } = new();

        private readonly Func<string, (HttpStatusCode, string, string? contentType)> _responder;
        private readonly Func<Exception>? _throw;

        public StubHandler(Func<string, (HttpStatusCode, string, string? contentType)> responder,
                           Func<Exception>? throwFactory = null)
        {
            _responder = responder;
            _throw = throwFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accept = request.Headers.Accept.Any()
                ? string.Join(", ", request.Headers.Accept.Select(a => a.MediaType))
                : null;
            Requests.Add(new Captured(request.Method, request.RequestUri!, accept));

            if (_throw != null)
                return Task.FromException<HttpResponseMessage>(_throw());

            var (status, body, contentType) = _responder(request.RequestUri!.ToString());
            var content = new StringContent(body, Encoding.UTF8);
            if (contentType != null)
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }

    private static OutboundWebFingerHarness Create(
        string webfingerBody, string actorBody,
        string? webfingerContentType = "application/activity+json",
        string? actorContentType = "application/activity+json")
    {
        var handler = new StubHandler(url =>
        {
            if (url.Contains("/.well-known/webfinger"))
                return (HttpStatusCode.OK, webfingerBody, webfingerContentType);
            return (HttpStatusCode.OK, actorBody, actorContentType);
        });
        var client = new HttpClient(handler);
        var service = new WebFingerService(client, new Mock<ILogger<WebFingerService>>().Object);
        return new OutboundWebFingerHarness(service, handler);
    }

    private static OutboundWebFingerHarness CreateWithHandler(StubHandler handler)
    {
        var client = new HttpClient(handler);
        var service = new WebFingerService(client, new Mock<ILogger<WebFingerService>>().Object);
        return new OutboundWebFingerHarness(service, handler);
    }

    private sealed record OutboundWebFingerHarness(WebFingerService Service, StubHandler Handler);

    private const string ActorJson = """
        {
          "id": "https://mastodon.world/users/RayvenMX",
          "type": "Person",
          "preferredUsername": "RayvenMX",
          "inbox": "https://mastodon.world/users/RayvenMX/inbox",
          "endpoints": { "sharedInbox": "https://mastodon.world/inbox" }
        }
        """;

    [Fact]
    public async Task ResolveActor_ValidSelfLink_ReturnsActor_AndSendsAcceptHeader()
    {
        const string webfinger = """
            {
              "subject": "acct:rayvenmx@mastodon.world",
              "links": [
                { "rel": "self", "type": "application/activity+json", "href": "https://mastodon.world/users/RayvenMX" },
                { "rel": "http://webfinger.net/rel/profile-page", "type": "text/html", "href": "https://mastodon.world/@RayvenMX" }
              ]
            }
            """;

        var harness = Create(webfinger, ActorJson);
        var actor = await harness.Service.ResolveActorAsync("rayvenmx@mastodon.world");

        Assert.NotNull(actor);
        Assert.Equal("RayvenMX", actor!.PreferredUsername);

        // The webfinger request went to the correct URL with the AP Accept header.
        var webfingerReq = Assert.Single(harness.Handler.Requests, r => r.Uri.ToString().Contains("/.well-known/webfinger"));
        Assert.StartsWith("https://mastodon.world/.well-known/webfinger", webfingerReq.Uri.ToString());

        // resource=acct:rayvenmx@mastodon.world (percent-encoded in the query)
        var resource = Uri.UnescapeDataString(webfingerReq.Uri.Query.Split("resource=", 2)[1]);
        Assert.Equal("acct:rayvenmx@mastodon.world", resource);

        Assert.NotNull(webfingerReq.Accept);
        Assert.Contains("application/activity+json", webfingerReq.Accept!, StringComparison.OrdinalIgnoreCase);

        // The actor fetch also carries the AP Accept header.
        var actorReq = Assert.Single(harness.Handler.Requests, r => r.Uri.ToString().Contains("/users/RayvenMX"));
        Assert.Contains("application/activity+json", actorReq.Accept!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveActor_SelfLinkWithoutType_FallsBackToBareSelf()
    {
        // A valid ActivityPub server that omits `type` on the self link.
        const string webfinger = """
            {
              "subject": "acct:alice@example.social",
              "links": [
                { "rel": "self", "href": "https://example.social/users/alice" },
                { "rel": "http://webfinger.net/rel/profile-page", "type": "text/html", "href": "https://example.social/@alice" }
              ]
            }
            """;

        var harness = Create(webfinger, ActorJson);
        var actor = await harness.Service.ResolveActorAsync("alice@example.social");

        Assert.NotNull(actor);
        Assert.Equal("RayvenMX", actor!.PreferredUsername);
    }

    [Fact]
    public async Task ResolveActor_PrefersTypedSelfLink_OverBareSelf()
    {
        // Two self links: a bare one and a typed one. The typed AP one wins.
        const string webfinger = """
            {
              "subject": "acct:bob@example.social",
              "links": [
                { "rel": "self", "href": "https://example.social/bare-actor" },
                { "rel": "self", "type": "application/activity+json", "href": "https://example.social/users/bob" }
              ]
            }
            """;

        var harness = Create(webfinger, ActorJson);
        await harness.Service.ResolveActorAsync("bob@example.social");

        var actorReq = Assert.Single(harness.Handler.Requests, r => r.Uri.ToString().Contains("/users/bob"));
        Assert.NotNull(actorReq);
        Assert.DoesNotContain(harness.Handler.Requests, r => r.Uri.ToString().Contains("/bare-actor"));
    }

    [Fact]
    public async Task ResolveActor_NoSelfLink_ReturnsNull()
    {
        const string webfinger = """
            {
              "subject": "acct:nobody@example.social",
              "links": [
                { "rel": "http://webfinger.net/rel/profile-page", "type": "text/html", "href": "https://example.social/@nobody" }
              ]
            }
            """;

        var harness = Create(webfinger, ActorJson);
        var actor = await harness.Service.ResolveActorAsync("nobody@example.social");

        Assert.Null(actor);
    }

    [Fact]
    public async Task ResolveActor_Webfinger404_ReturnsNull()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.NotFound, "", null));
        var service = new WebFingerService(new HttpClient(handler), new Mock<ILogger<WebFingerService>>().Object);

        var actor = await service.ResolveActorAsync("ghost@mastodon.world");
        Assert.Null(actor);
    }

    [Fact]
    public async Task ResolveActor_InvalidHandle_ReturnsNull_AndMakesNoRequest()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.OK, "", null));
        var service = new WebFingerService(new HttpClient(handler), new Mock<ILogger<WebFingerService>>().Object);

        Assert.Null(await service.ResolveActorAsync("no-at-sign"));
        Assert.Null(await service.ResolveActorAsync("@missing-user"));
        Assert.Empty(handler.Requests);
    }

    // --- Real-world content-shape regressions ----------------------------
    // These pin the exact shapes real Mastodon instances return, which the old
    // code could not handle and which caused "cannot communicate with the
    // outside world."

    [Fact]
    public async Task ResolveActor_WebfingerServedAsJrd_IsStillParsed()
    {
        // Mastodon serves webfinger with content-type application/jrd+json.
        // The old ReadFromJsonAsync<JsonNode>() rejected a 200 whose content
        // type it didn't recognize; we now parse the raw body.
        const string webfinger = """
            { "subject": "acct:rayvenmx@mastodon.world",
              "links": [ { "rel": "self", "type": "application/activity+json", "href": "https://mastodon.world/users/RayvenMX" } ] }
            """;

        var harness = Create(webfinger, ActorJson,
            webfingerContentType: "application/jrd+json");
        var actor = await harness.Service.ResolveActorAsync("RayvenMX@mastodon.world");

        Assert.NotNull(actor);
        Assert.Equal("RayvenMX", actor!.PreferredUsername);
    }

    [Fact]
    public async Task ResolveActor_ActorWithArrayContext_Parses()
    {
        // A real Mastodon actor document has "@context" as an *array*, not a
        // string. The old string-typed Context property threw "cannot convert
        // StartArray to string" and discarded the whole document.
        const string arrayContextActor = """
            {
              "@context": ["https://www.w3.org/ns/activitystreams", "https://w3id.org/security/v1"],
              "id": "https://mastodon.world/users/RayvenMX",
              "type": "Person",
              "preferredUsername": "RayvenMX",
              "inbox": "https://mastodon.world/users/RayvenMX/inbox",
              "endpoints": { "sharedInbox": "https://mastodon.world/inbox" }
            }
            """;

        const string webfinger = """
            { "subject": "acct:rayvenmx@mastodon.world",
              "links": [ { "rel": "self", "type": "application/activity+json", "href": "https://mastodon.world/users/RayvenMX" } ] }
            """;

        var harness = Create(webfinger, arrayContextActor);
        var actor = await harness.Service.ResolveActorAsync("RayvenMX@mastodon.world");

        Assert.NotNull(actor);
        Assert.Equal("RayvenMX", actor!.PreferredUsername);
        Assert.Equal("https://mastodon.world/users/RayvenMX/inbox", actor.Inbox);
        Assert.Equal("https://mastodon.world/inbox", actor.Endpoints?.SharedInbox);
    }

    [Fact]
    public async Task ResolveActor_WebfingerAsJrd_And_ActorWithArrayContext_Resolves()
    {
        // The combination real Mastodon produces: JRD webfinger + array-context
        // actor. This is the full "outside world" shape.
        const string webfinger = """
            { "subject": "acct:rayvenmx@mastodon.world",
              "links": [ { "rel": "self", "type": "application/activity+json", "href": "https://mastodon.world/users/RayvenMX" } ] }
            """;
        const string arrayContextActor = """
            {
              "@context": ["https://www.w3.org/ns/activitystreams", "https://w3id.org/security/v1"],
              "id": "https://mastodon.world/users/RayvenMX",
              "type": "Person",
              "preferredUsername": "RayvenMX",
              "endpoints": { "sharedInbox": "https://mastodon.world/inbox" }
            }
            """;

        var harness = Create(webfinger, arrayContextActor,
            webfingerContentType: "application/jrd+json");
        var actor = await harness.Service.ResolveActorAsync("RayvenMX@mastodon.world");

        Assert.NotNull(actor);
        Assert.Equal("RayvenMX", actor!.PreferredUsername);
        Assert.Equal("https://mastodon.world/inbox", actor.Endpoints?.SharedInbox);
    }

    // --- Graceful remote-failure contract --------------------------------
    // Federation must NEVER throw on a misbehaving remote: a 5xx, a
    // connection reset, or a timeout all degrade to `null` (logged), so a
    // single bad instance can't take down the whole app.

    [Fact]
    public async Task ResolveActor_Webfinger500_ReturnsNull_DoesNotThrow()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.InternalServerError, "oops", "text/plain"));
        var harness = CreateWithHandler(handler);

        var actor = await harness.Service.ResolveActorAsync("RayvenMX@mastodon.world");

        Assert.Null(actor);
    }

    [Fact]
    public async Task ResolveActor_ActorFetch500_ReturnsNull_DoesNotThrow()
    {
        // Webfinger succeeds (points at a self link) but the actor document
        // itself returns a 5xx — still must degrade to null, not throw.
        const string webfinger = """
            { "subject": "acct:rayvenmx@mastodon.world",
              "links": [ { "rel": "self", "type": "application/activity+json", "href": "https://mastodon.world/users/RayvenMX" } ] }
            """;
        var handler = new StubHandler(url =>
            url.Contains("/.well-known/webfinger")
                ? (HttpStatusCode.OK, webfinger, "application/jrd+json")
                : (HttpStatusCode.BadGateway, "bad gateway", "text/plain"));
        var harness = CreateWithHandler(handler);

        var actor = await harness.Service.ResolveActorAsync("RayvenMX@mastodon.world");

        Assert.Null(actor);
    }

    [Fact]
    public async Task ResolveActor_ConnectorFailure_ReturnsNull_DoesNotThrow()
    {
        // A DNS failure / connection reset surfaces as HttpRequestException.
        var handler = new StubHandler(_ => (HttpStatusCode.OK, "", null),
            () => new HttpRequestException("connection reset by peer"));
        var harness = CreateWithHandler(handler);

        var actor = await harness.Service.ResolveActorAsync("RayvenMX@mastodon.world");

        Assert.Null(actor);
    }

    [Fact]
    public async Task ResolveActor_Timeout_ReturnsNull_DoesNotThrow()
    {
        // .NET's HttpClient surfaces a timeout as TaskCanceledException.
        var handler = new StubHandler(_ => (HttpStatusCode.OK, "", null),
            () => new TaskCanceledException("The operation was canceled."));
        var harness = CreateWithHandler(handler);

        var actor = await harness.Service.ResolveActorAsync("RayvenMX@mastodon.world");

        Assert.Null(actor);
    }

    [Fact]
    public async Task ResolveActor_MalformedJson_ReturnsNull_DoesNotThrow()
    {
        // A 200 whose body is not JSON must not throw out of the resolver.
        var handler = new StubHandler(_ => (HttpStatusCode.OK, "<html>not json</html>", "text/html"));
        var harness = CreateWithHandler(handler);

        var actor = await harness.Service.ResolveActorAsync("RayvenMX@mastodon.world");

        Assert.Null(actor);
    }
}
