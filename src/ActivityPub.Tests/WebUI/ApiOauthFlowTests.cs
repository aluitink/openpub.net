using System.Net;
using System.Security.Cryptography;
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
/// Tests for the OAuth 2.0 authorization-code + PKCE flow used for API
/// authentication: POST /api/v1/apps (register a client),
/// GET /api/v1/oauth/authorize (issue a code),
/// POST /api/v1/oauth/token (exchange for a bearer token), and using that
/// token to authenticate against protected API endpoints.
/// </summary>
public class ApiOauthFlowTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ApiOauthFlowTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>
    /// A client that does NOT follow redirects, so the 302 from the authorize
    /// endpoint is observed directly (instead of being followed to the client's
    /// redirect_uri, which is not routable in the test host). Cookies are still
    /// handled so an established session is preserved.
    /// </summary>
    HttpClient CreateNoRedirectClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
        => new FormUrlEncodedContent(fields);

    static StringContent JsonBody(object payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    async Task<HttpClient> GetAuthenticatedClient()
    {
        var client = CreateNoRedirectClient();
        var username = $"oauth_{Guid.NewGuid().ToString("N")[..8]}";
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "OAuth Test User" },
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

    static async Task<string> RegisterApp(HttpClient client)
    {
        var response = await client.PostAsync("/api/v1/apps", JsonBody(new
        {
            name = "OAuth Flow Test App",
            redirect_uris = "https://client.example.com/callback",
            scopes = "read write"
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("client_id").GetString()!;
    }

    // ---------- Authorize endpoint validation ----------

    [Fact]
    public async Task Authorize_WrongResponseType_Returns400()
    {
        var client = await GetAuthenticatedClient();
        var clientId = await RegisterApp(client);

        var response = await client.GetAsync(
            $"/api/v1/oauth/authorize?response_type=token&client_id={clientId}&redirect_uri=https://client.example.com/callback");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_UnknownClient_Returns400()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync(
            "/api/v1/oauth/authorize?response_type=code&client_id=doesnotexist&redirect_uri=https://client.example.com/callback");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_WithoutAuthentication_Returns401()
    {
        var client = CreateClient();
        var response = await client.GetAsync(
            "/api/v1/oauth/authorize?response_type=code&client_id=whatever&redirect_uri=https://client.example.com/callback");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Full flow ----------

    [Fact]
    public async Task FullFlow_IssueCodeAndToken()
    {
        var client = await GetAuthenticatedClient();
        var clientId = await RegisterApp(client);

        // 1) Authorize (with PKCE challenge).
        var (verifier, challenge) = MakePkcePair();
        var authResponse = await client.GetAsync(
            $"/api/v1/oauth/authorize?response_type=code&client_id={clientId}" +
            $"&redirect_uri=https://client.example.com/callback&scope=read%20write" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}&code_challenge_method=S256");
        Assert.Equal(HttpStatusCode.Redirect, authResponse.StatusCode);

        var location = authResponse.Headers.Location!.ToString();
        Assert.Contains("code=", location);

        // 2) Token exchange.
        var tokenResponse = await client.PostAsync("/api/v1/oauth/token", CreateFormContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", ExtractCode(location) },
            { "client_id", clientId },
            { "code_verifier", verifier },
            { "redirect_uri", "https://client.example.com/callback" },
        }));
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var doc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken), "access_token should be present");
        Assert.Equal("Bearer", root.GetProperty("token_type").GetString());

        // 3) Use the bearer token on a protected endpoint.
        var api = CreateClient();
        api.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var homeResponse = await api.GetAsync("/api/v1/timelines/home");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
    }

    [Fact]
    public async Task TokenExchange_WrongVerifier_Returns400()
    {
        var client = await GetAuthenticatedClient();
        var clientId = await RegisterApp(client);

        var (verifier, challenge) = MakePkcePair();
        var authResponse = await client.GetAsync(
            $"/api/v1/oauth/authorize?response_type=code&client_id={clientId}" +
            $"&redirect_uri=https://client.example.com/callback" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}&code_challenge_method=S256");
        var location = authResponse.Headers.Location!.ToString();

        var tokenResponse = await client.PostAsync("/api/v1/oauth/token", CreateFormContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", ExtractCode(location) },
            { "client_id", clientId },
            { "code_verifier", "wrong-verifier-value" },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, tokenResponse.StatusCode);
    }

    [Fact]
    public async Task TokenExchange_CodeReplay_Returns400()
    {
        var client = await GetAuthenticatedClient();
        var clientId = await RegisterApp(client);

        // No PKCE for this flow (client registered without requiring it).
        var authResponse = await client.GetAsync(
            $"/api/v1/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri=https://client.example.com/callback");
        var location = authResponse.Headers.Location!.ToString();
        var code = ExtractCode(location);

        // First exchange succeeds.
        var first = await client.PostAsync("/api/v1/oauth/token", CreateFormContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "client_id", clientId },
        }));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second (replay) must fail — the code is single-use.
        var second = await client.PostAsync("/api/v1/oauth/token", CreateFormContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "client_id", clientId },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task BearerToken_Invalid_Returns401()
    {
        var api = CreateClient();
        api.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-real-token");
        var response = await api.GetAsync("/api/v1/timelines/home");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- PKCE helpers ----------

    static (string verifier, string challenge) MakePkcePair()
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        var challenge = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (verifier, challenge);
    }

    static string ExtractCode(string location)
    {
        var idx = location.IndexOf("code=", StringComparison.OrdinalIgnoreCase);
        Assert.True(idx >= 0, $"No code= in redirect location: {location}");
        var code = location[(idx + "code=".Length)..];
        var amp = code.IndexOf('&');
        if (amp >= 0) code = code[..amp];
        return Uri.UnescapeDataString(code);
    }
}
