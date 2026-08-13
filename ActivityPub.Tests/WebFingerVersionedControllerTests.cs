using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core;
using Microsoft.AspNetCore.Mvc.Versioning;
using System.Text.Json;

namespace ActivityPub.Core.Tests;

public class WebFingerVersionedControllerTests
{
    [Fact]
    public async Task WebFingerVersioned_Returns_Valid_JRD_For_Acct_Resource_With_Version_Header()
    {
        // Arrange
        var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();
        
        // Act - Test with version header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/webfinger?resource=acct:test@localhost");
        request.Headers.Add("api-version", "1.0");
        var response = await client.SendAsync(request);
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("{\"subject\":\"acct:test@localhost\",\"links\":[{\"rel\":\"self\",\"type\":\"application/activity+json\",\"href\":\"/users/test\"}]}", content);
    }

    [Fact]
    public async Task WebFingerVersioned_Returns_Valid_JRD_For_Acct_Resource_With_Url_Version()
    {
        // Arrange
        var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();
        
        // Act - Test with version in URL
        var response = await client.GetAsync("/api/v1/webfinger?resource=acct:test@localhost");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"subject\":\"acct:test@localhost\"", content);
        Assert.Contains("\"rel\":\"self\"", content);
        Assert.Contains("\"type\":\"application/activity+json\"", content);
    }

    [Fact]
    public async Task WebFingerVersioned_Invalid_Resource_Returns_BadRequest()
    {
        // Arrange
        var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();
        
        // Act - Test with missing resource parameter
        var response = await client.GetAsync("/api/v1/webfinger");
        
        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(400, (int)response.StatusCode);
    }
}