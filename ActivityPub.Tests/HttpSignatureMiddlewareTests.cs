using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using Xunit;
using ActivityPub.Core;

namespace ActivityPub.Tests;

public class HttpSignatureMiddlewareTests
{
    [Fact]
    public async Task Http_Signature_Middleware_Test()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/http-signature-test");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        // Add assertions as needed
    }
}