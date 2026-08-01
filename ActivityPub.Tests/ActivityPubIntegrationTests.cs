using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using Xunit;
using ActivityPub.Core;

namespace ActivityPub.Tests;

public class ActivityPubIntegrationTests
{
    [Fact]
    public async Task ActivityPub_Integration_Test()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/activitypub/integration");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        // Add assertions as needed
    }
}