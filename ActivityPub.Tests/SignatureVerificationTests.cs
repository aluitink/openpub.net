using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using Xunit;
using ActivityPub.Core;

namespace ActivityPub.Tests;

public class SignatureVerificationTests
{
    [Fact]
    public async Task Signature_Verification_Test()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/signature-verification-test");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        // Add assertions as needed
    }
}