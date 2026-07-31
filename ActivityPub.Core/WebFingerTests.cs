using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using Xunit;

namespace ActivityPub.Core.Tests;

public class WebFingerTests
{
    [Fact]
    public async Task WebFinger_Returns_Valid_JRD_For_Acct_Resource()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:test@localhost");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"subject\":\"acct:test@localhost\"", content);
        Assert.Contains("\"rel\":\"self\"", content);
    }
}