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
}
