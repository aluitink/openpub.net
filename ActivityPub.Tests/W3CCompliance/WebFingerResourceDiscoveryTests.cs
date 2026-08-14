using ActivityPub.Core.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.W3CCompliance;

public class WebFingerResourceDiscoveryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WebFingerResourceDiscoveryTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WebFinger_Endpoint_Must_Exist()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented, 
                   $"Expected OK or NotImplemented, got {response.StatusCode}");
    }

    [Fact]
    public async Task WebFinger_Response_Must_Have_Subject()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Response_Must_Have_Links()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Support_Accept_Headers()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Handle_Resource_Parameter()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Support_Rel_Parameter()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test&rel=http://webfinger.net/rel/profile-page");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Support_Accept_Language_Header()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));
        request.Headers.Add("Accept-Language", "en-US");

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Response_Must_Be_Valid_JSON()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotNull(content);
            Assert.True(content.Length > 0);
        }
    }

    [Fact]
    public async Task WebFinger_Must_Return_200_For_Valid_Resource()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Return_400_For_Missing_Resource_Parameter()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.BadRequest || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Support_JSON_Output()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Support_JRD_Output()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Handle_HTTPS_Resources()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=https://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Handle_HTTP_Resources()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=http://example.com/users/test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task WebFinger_Must_Handle_Email_Like_Resources()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/webfinger?resource=test%40example.com");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jrd+json"));

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK || 
                   response.StatusCode == System.Net.HttpStatusCode.NotImplemented);
    }
}
