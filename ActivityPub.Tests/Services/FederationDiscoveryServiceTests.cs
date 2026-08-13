using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ActivityPub.Tests.Services;

public class FederationDiscoveryServiceTests
{
    private readonly ILogger<FederationDiscoveryService> _logger;
    private readonly FederationDiscoveryService _service;

    public FederationDiscoveryServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<FederationDiscoveryService>();
        _service = new FederationDiscoveryService(_logger);
    }

    [Fact]
    public async Task DiscoverEndpointAsync_ValidDomain_ReturnsHttpsUrl()
    {
        // Act
        var result = await _service.DiscoverEndpointAsync("example.com");

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("https://", result!);
    }

    [Fact]
    public async Task DiscoverEndpointAsync_NullDomain_ReturnsNull()
    {
        // Act
        var result = await _service.DiscoverEndpointAsync(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DiscoverEndpointAsync_EmptyDomain_ReturnsNull()
    {
        // Act
        var result = await _service.DiscoverEndpointAsync(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DiscoverEndpointAsync_WhitespaceDomain_ReturnsNull()
    {
        // Act
        var result = await _service.DiscoverEndpointAsync("   ");

        // Assert
        Assert.Null(result);
    }
}
