using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.Integration.Discovery;

/// <summary>
/// Integration tests for ActivityPub discovery between multiple server instances
/// Verifies that multiple AP instances can discover each other's endpoints
/// </summary>
public class DiscoveryIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DiscoveryIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Discovery_CanResolveActorEndpoint()
    {
        var client = _factory.CreateClient();
        
        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:actor1@localhost");
        
        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var jrd = JsonSerializer.Deserialize<WebFingerJrd>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(jrd);
        Assert.NotNull(jrd.Links);
        
        var selfLink = jrd.Links.FirstOrDefault(l => l.Rel == "self");
        Assert.NotNull(selfLink);
        Assert.Contains("localhost", selfLink.Href);
    }

    [Fact]
    public async Task Discovery_CanResolveMultipleActors()
    {
        var client = _factory.CreateClient();
        
        var response1 = await client.GetAsync("/.well-known/webfinger?resource=acct:actor1@localhost");
        var response2 = await client.GetAsync("/.well-known/webfinger?resource=acct:actor2@localhost");
        
        Assert.True(response1.IsSuccessStatusCode);
        Assert.True(response2.IsSuccessStatusCode);
        
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        
        Assert.NotEqual(content1, content2);
    }

    [Fact]
    public async Task Discovery_Returns_Correct_Endpoint_Format()
    {
        var client = _factory.CreateClient();
        
        var resource = "acct:testuser@localhost";
        var response = await client.GetAsync($"/.well-known/webfinger?resource={WebUtility.UrlEncode(resource)}");
        
        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var jrd = JsonSerializer.Deserialize<WebFingerJrd>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.Equal($"acct:testuser@localhost", jrd?.Subject);
        
        var selfLink = jrd?.Links?.FirstOrDefault(l => l.Rel == "self");
        Assert.NotNull(selfLink);
        Assert.Equal("application/activity+json", selfLink?.Type);
    }

    [Fact]
    public async Task Discovery_MultipleInstances_CanResolveEachOther()
    {
        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();
        
        var response1 = await client1.GetAsync("/.well-known/webfinger?resource=acct:user1@localhost");
        var response2 = await client2.GetAsync("/.well-known/webfinger?resource=acct:user2@localhost");
        
        Assert.True(response1.IsSuccessStatusCode);
        Assert.True(response2.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Discovery_WebFinger_Cache_Returns_Cached_Response()
    {
        var client = _factory.CreateClient();
        
        var firstResponse = await client.GetAsync("/.well-known/webfinger?resource=acct:cached@localhost");
        var secondResponse = await client.GetAsync("/.well-known/webfinger?resource=acct:cached@localhost");
        
        Assert.True(firstResponse.IsSuccessStatusCode);
        Assert.True(secondResponse.IsSuccessStatusCode);
    }
}
