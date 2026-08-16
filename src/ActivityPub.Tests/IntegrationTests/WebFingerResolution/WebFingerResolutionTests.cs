using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.IntegrationTests.WebFingerResolution;

public class WebFingerResolutionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public WebFingerResolutionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WebFinger_Returns_User_Information()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:testuser@localhost");

        Assert.True(response.IsSuccessStatusCode, "WebFinger should return user info");

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("subject", content);
        Assert.Contains("acct:testuser@localhost", content);
    }

    [Fact]
    public async Task WebFinger_Supports_Accept_Header()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=acct:testuser@localhost");
        request.Headers.Accept.ParseAdd("application/jrd+json");

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, "WebFinger should respect accept header");
        Assert.Contains("application/jrd+json", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task WebFinger_Returns_Host_Document()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/.well-known/webfinger?resource=https://localhost");

        Assert.True(response.IsSuccessStatusCode, "WebFinger should handle host document request");
    }

    [Fact]
    public async Task WebFinger_Supports_Accept_Application_Xrd()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=acct:testuser@localhost");
        request.Headers.Accept.ParseAdd("application/xrd+json");

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, "WebFinger should support XRD format");
    }

    [Fact]
    public async Task WebFinger_With_Empty_Resource_Returns_Error()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/.well-known/webfinger");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
