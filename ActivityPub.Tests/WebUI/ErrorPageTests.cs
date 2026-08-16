using ActivityPub.Tests.WebUI;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class ErrorPageTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ErrorPageTests(WebUIFactory factory)
    {
        _factory = factory;
    }

    HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task NotFoundPage_Returns404()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/nonexistent-page-12345");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NotFoundPage_RendersNotFoundView()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/nonexistent-page-12345");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Page Not Found", body);
    }

    [Fact]
    public async Task NotFoundPage_ShowsHomeLink()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/nonexistent-page-12345");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Go Home", body);
    }

    [Fact]
    public async Task ErrorEndpoint_Returns500()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/Home/Error");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("An Error Occurred", body);
    }

    [Fact]
    public async Task ErrorPage_ShowsErrorStatus()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/Home/Error");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Error 500", body);
    }

    [Fact]
    public async Task ErrorPage_ShowsActions()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/Home/Error");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Go Home", body);
        Assert.Contains("Go Back", body);
    }
}
