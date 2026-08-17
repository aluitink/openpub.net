using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Web-hosted tests for API documentation (Swagger UI + OpenAPI JSON). The
/// OpenAPI document is public and describes the /api/v1 REST contract; the
/// Swagger UI links to it.
/// </summary>
public class ApiSwaggerTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ApiSwaggerTests(WebUIFactory factory)
    {
        _factory = factory;
    }

    HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task OpenApiJson_Returns200WithValidDocument()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Must be a valid OpenAPI 3.x document.
        Assert.True(root.TryGetProperty("openapi", out var openapi), "openapi version missing");
        Assert.StartsWith("3.", openapi.GetString());

        // Title is set.
        Assert.True(root.TryGetProperty("info", out var info), "info missing");
        Assert.Contains("REST API", info.GetProperty("title").GetString());

        // The API paths are described.
        Assert.True(root.TryGetProperty("paths", out var paths), "paths missing");
        Assert.True(paths.TryGetProperty("/api/v1/statuses", out _), "statuses path missing");
        Assert.True(paths.TryGetProperty("/api/v1/accounts", out _), "accounts path missing");
        Assert.True(paths.TryGetProperty("/api/v1/timelines/home", out _), "home timeline path missing");
        Assert.True(paths.TryGetProperty("/api/v1/apps", out _), "apps path missing");
        Assert.True(paths.TryGetProperty("/api/v1/oauth/token", out _), "oauth token path missing");

        // Both security schemes are advertised.
        Assert.True(root.TryGetProperty("components", out var components), "components missing");
        Assert.True(components.TryGetProperty("securitySchemes", out var schemes), "securitySchemes missing");
        Assert.True(schemes.TryGetProperty("Bearer", out _), "Bearer scheme missing");
        Assert.True(schemes.TryGetProperty("Cookies", out _), "Cookies scheme missing");
    }

    [Fact]
    public async Task SwaggerUi_Returns200()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Swagger UI page loads the bundled swagger-ui assets.
        Assert.Contains("swagger-ui", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SwaggerIndex_Root_Returns200OrRedirect()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/swagger");

        // /swagger should serve the UI directly or redirect to /swagger/index.html.
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently or HttpStatusCode.Found,
            $"/swagger returned {(int)response.StatusCode}");
    }
}
