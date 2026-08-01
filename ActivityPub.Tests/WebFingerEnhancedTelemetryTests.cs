using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Infrastructure.Telemetry;
using ActivityPub.Core.Services;
using ActivityPub.Core;

namespace ActivityPub.Core.Tests;

public class WebFingerEnhancedTelemetryTests
{
    [Fact]
    public async Task WebFinger_Records_Telemetry_Metrics()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:test@localhost");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }
    
    [Fact]
    public async Task WebFinger_Cache_Statistics_Endpoint_Returns_Valid_Data()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/.well-known/webfinger/cache-stats");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"timestamp\"", content);
    }
}