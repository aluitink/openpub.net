using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ActivityPub.Tests.Services;

public class OutboundSigningServiceTests
{
    private readonly ILogger<OutboundSigningService> _logger;
    private readonly OutboundSigningService _service;

    public OutboundSigningServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<OutboundSigningService>();
        _service = new OutboundSigningService(_logger);
    }

    [Fact]
    public void SignRequest_NullRequest_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.SignRequest(null!, "key", "id", "host"));
    }

    [Fact]
    public void SignRequest_NullPrivateKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.SignRequest(new HttpRequestMessage(), null!, "id", "host"));
    }

    [Fact]
    public void SignRequest_NullKeyId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.SignRequest(new HttpRequestMessage(), "key", null!, "host"));
    }

    [Fact]
    public void SignRequest_NullHostname_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.SignRequest(new HttpRequestMessage(), "key", "id", null!));
    }
}
