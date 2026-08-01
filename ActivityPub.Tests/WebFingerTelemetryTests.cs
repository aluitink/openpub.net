using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Text.Json;
using Xunit;
using ActivityPub.Core.Infrastructure.Telemetry;
using ActivityPub.Core.Models;
using System.Threading.Tasks;
using System.Diagnostics;
using ActivityPub.Core;

namespace ActivityPub.Tests;

public class WebFingerTelemetryTests
{
    [Fact]
    public async Task WebFinger_Telemetry_Test()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:telemetry@localhost");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        
        // Parse the JSON to validate the strongly-typed JRD structure
        var jrd = JsonSerializer.Deserialize<WebFingerJrd>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(jrd);
        Assert.Equal("acct:telemetry@localhost", jrd.Subject);
        Assert.NotEmpty(jrd.Links);
        Assert.Equal("self", jrd.Links[0].Rel);
        Assert.Equal("application/activity+json", jrd.Links[0].Type);
        Assert.NotNull(jrd.Links[0].Href);
    }
}