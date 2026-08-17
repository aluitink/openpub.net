using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Tests for the Mastodon-compatible application registration flow
/// (POST /api/v1/apps) and the authenticated client list (GET /api/v1/apps).
/// </summary>
public class ApiAppRegistrationTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ApiAppRegistrationTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
        => new FormUrlEncodedContent(fields);

    async Task<HttpClient> GetAuthenticatedClient()
    {
        var client = CreateClient();
        var username = $"app_{Guid.NewGuid().ToString("N")[..8]}";
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "App Test User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(registerResponse.IsSuccessStatusCode || registerResponse.Headers.Location != null,
            $"Register failed: {(int)registerResponse.StatusCode}");

        var loginResponse = await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        Assert.True(loginResponse.IsSuccessStatusCode || loginResponse.Headers.Location != null,
            $"Login failed: {(int)loginResponse.StatusCode}");

        return client;
    }

    static StringContent JsonBody(object payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Register_ReturnsClientIdAndSecret()
    {
        var client = CreateClient();
        var response = await client.PostAsync("/api/v1/apps", JsonBody(new
        {
            name = "My Test App",
            redirect_uris = "urn:ietf:wg:oauth:2.0:oob https://example.com/callback",
            scopes = "read write",
            website = "https://example.com"
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("My Test App", root.GetProperty("name").GetString());
        Assert.Equal("https://example.com", root.GetProperty("website").GetString());

        var clientId = root.GetProperty("client_id").GetString();
        var clientSecret = root.GetProperty("client_secret").GetString();

        Assert.False(string.IsNullOrWhiteSpace(clientId), "client_id should be present");
        Assert.False(string.IsNullOrWhiteSpace(clientSecret), "client_secret should be present");
        Assert.True(clientId != clientSecret, "client_id and client_secret should differ");
        Assert.True(root.TryGetProperty("redirect_uri", out _));
        Assert.Equal("urn:ietf:wg:oauth:2.0:oob", root.GetProperty("redirect_uri").GetString());
    }

    [Fact]
    public async Task Register_WithDefaults_FillsNameAndScopes()
    {
        var client = CreateClient();
        var response = await client.PostAsync("/api/v1/apps", JsonBody(new { }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Unnamed application", root.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("client_id").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("client_secret").GetString()));
    }

    [Fact]
    public async Task Register_SecondApp_GetsDifferentCredentials()
    {
        var client = CreateClient();

        var r1 = await client.PostAsync("/api/v1/apps", JsonBody(new { name = "App One" }));
        var r2 = await client.PostAsync("/api/v1/apps", JsonBody(new { name = "App Two" }));

        var id1 = JsonDocument.Parse(await r1.Content.ReadAsStringAsync()).RootElement.GetProperty("client_id").GetString()!;
        var id2 = JsonDocument.Parse(await r2.Content.ReadAsStringAsync()).RootElement.GetProperty("client_id").GetString()!;

        Assert.True(id1 != id2, "Each registration should get a unique client_id");
    }

    [Fact]
    public async Task List_WithoutAuthentication_Returns401()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/v1/apps");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_Authed_ReturnsRegisteredApps()
    {
        var client = await GetAuthenticatedClient();

        // Register an application as this user (authenticated).
        var registerResponse = await client.PostAsync("/api/v1/apps", JsonBody(new { name = "My Owned App" }));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registeredClientId = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("client_id").GetString()!;

        var listResponse = await client.GetAsync("/api/v1/apps");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var json = await listResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var array = doc.RootElement;

        Assert.True(array.GetArrayLength() >= 1, "Expected at least one registered app");

        var found = false;
        foreach (var app in array.EnumerateArray())
        {
            if (app.GetProperty("client_id").GetString() == registeredClientId)
            {
                found = true;
                Assert.Equal("My Owned App", app.GetProperty("name").GetString());
                // The secret must never be re-emitted in the list.
                Assert.True(!app.TryGetProperty("client_secret", out _), "client_secret must not appear in GET /api/v1/apps");
            }
        }
        Assert.True(found, "The registered app should appear in the list");
    }
}
