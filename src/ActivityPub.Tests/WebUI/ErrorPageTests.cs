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

    // --- Status-code re-execution (Phase 49.5 on-brand error pages) ---

    [Fact]
    public async Task StatusCode404_ReExecutesStatusErrorPage()
    {
        // UseStatusCodePagesWithReExecute("/Home/StatusError", "?id={0}") re-runs
        // the request for any non-2xx status. The response keeps the original
        // status code while rendering the on-brand page.
        var client = CreateClient();
        var response = await client.GetAsync("/definitely-not-a-real-route-xyz");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Page Not Found", body);
        Assert.Contains("Error 404", body);
        // On-brand inline-SVG icon, not an emoji glyph.
        Assert.Contains("<svg", body);
    }

    [Fact]
    public async Task StatusErrorAction_Renders404ByDefault()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/Home/StatusError");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Page Not Found", body);
        Assert.Contains("Error 404", body);
        Assert.Contains("Go to Timeline", body);
        Assert.Contains("Go Home", body);
    }

    [Fact]
    public async Task StatusErrorAction_Renders403WhenRequested()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/Home/StatusError?id=403");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Access Denied", body);
        Assert.Contains("Error 403", body);
        Assert.Contains("Go Home", body);
    }

    [Fact]
    public async Task StatusErrorAction_Renders503WhenRequested()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/Home/StatusError?id=503");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Service Unavailable", body);
        Assert.Contains("Error 503", body);
    }

    [Fact]
    public async Task ForbiddenRoute_Renders403Page()
    {
        // Direct, on-brand 403 page at /Home/Forbidden.
        var client = CreateClient();
        var response = await client.GetAsync("/Home/Forbidden");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Access Denied", body);
        Assert.Contains("Error 403", body);
        Assert.Contains("<svg", body);
        Assert.Contains("Go Home", body);
        Assert.Contains("Go Back", body);
    }

    [Fact]
    public async Task StatusError_UnknownCode_RendersGenericPage()
    {
        // A 5xx-ish code that has no dedicated branch falls back to a generic
        // "Something Went Wrong" 500-style page while preserving the code.
        var client = CreateClient();
        var response = await client.GetAsync("/Home/StatusError?id=599");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Something Went Wrong", body);
        Assert.Contains("Error 599", body);
    }
}
