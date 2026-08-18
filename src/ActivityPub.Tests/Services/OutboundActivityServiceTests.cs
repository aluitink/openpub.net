using System.Net;
using System.Text;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

public class OutboundActivityServiceTests
{
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<IFederationDiscoveryService> _federationDiscoveryMock;
    private readonly Mock<IOutboundSigningService> _signingServiceMock;
    private readonly Mock<ILogger<OutboundActivityService>> _loggerMock;
    private readonly OutboundActivityService _service;

    public OutboundActivityServiceTests()
    {
        _httpClientMock = new Mock<HttpClient>();
        _federationDiscoveryMock = new Mock<IFederationDiscoveryService>();
        _signingServiceMock = new Mock<IOutboundSigningService>();
        _loggerMock = new Mock<ILogger<OutboundActivityService>>();

        _service = new OutboundActivityService(
            _httpClientMock.Object,
            _federationDiscoveryMock.Object,
            _signingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SendActivityAsync_NullActivity_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SendActivityAsync(null!, "actor", "key", "to"));
    }

    [Fact]
    public async Task SendActivityAsync_NullActorId_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SendActivityAsync("activity", null!, "key", "to"));
    }

    [Fact]
    public async Task SendActivityAsync_NullPrivateKey_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SendActivityAsync("activity", "actor", null!, "to"));
    }

    [Fact]
    public async Task SendActivityAsync_NullTo_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SendActivityAsync("activity", "actor", "key", null!));
    }

    // --- Inbox-honoring regression tests ---------------------------------
    // These use a real (stub) HttpClient so the *actual* POST URL is observed,
    // proving we deliver to the recipient's resolved inbox / sharedInbox rather
    // than re-deriving `{domain}/inbox`.

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Sent { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }

    private static (OutboundActivityService Service, CapturingHandler Handler) CreateRealService()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var service = new OutboundActivityService(
            httpClient,
            new Mock<IFederationDiscoveryService>().Object,
            new Mock<IOutboundSigningService>().Object,
            new Mock<ILogger<OutboundActivityService>>().Object);
        return (service, handler);
    }

    [Fact]
    public async Task SendActivityAsync_HonorsResolvedSharedInbox()
    {
        var (service, handler) = CreateRealService();
        var ok = await service.SendActivityAsync("{}", "https://me.example/users/alice", "PEM",
            "https://mastodon.world/inbox");

        Assert.True(ok);
        var sent = Assert.Single(handler.Sent);
        Assert.Equal("https://mastodon.world/inbox", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendActivityAsync_HonorsPerUserInbox_NotDomainDefault()
    {
        // A deployment whose inbox is NOT at the domain root (Pleroma/Akkoma
        // per-user inbox). The old code would have POSTed to
        // https://example.social/inbox; we must hit the real per-user inbox.
        var (service, handler) = CreateRealService();
        var ok = await service.SendActivityAsync("{}", "https://me.example/users/alice", "PEM",
            "https://example.social/users/bob/inbox");

        Assert.True(ok);
        var sent = Assert.Single(handler.Sent);
        Assert.Equal("https://example.social/users/bob/inbox", sent.RequestUri!.ToString());
        Assert.DoesNotContain("https://example.social/inbox", handler.Sent.Select(r => r.RequestUri!.ToString()));
    }

    [Fact]
    public async Task SendActivityAsync_SignsForInboxHost()
    {
        // The signature must be built for the inbox's host (here example.social),
        // not the sender's host.
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var signing = new Mock<IOutboundSigningService>();
        var service = new OutboundActivityService(
            httpClient,
            new Mock<IFederationDiscoveryService>().Object,
            signing.Object,
            new Mock<ILogger<OutboundActivityService>>().Object);

        await service.SendActivityAsync("{}", "https://me.example/users/alice", "PEM",
            "https://example.social/users/bob/inbox");

        signing.Verify(s => s.SignRequest(
            It.IsAny<HttpRequestMessage>(), "PEM", "https://me.example/users/alice#main-key", "example.social"),
            Times.Once);
    }
}
