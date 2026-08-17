using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Web-hosted tests for API (Mastodon REST) rate limiting: the middleware
/// emits <c>RateLimit-*</c> headers on API responses and returns 429 when a
/// client exceeds its configured limit.
/// </summary>
public class ApiRateLimitingTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ApiRateLimitingTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
        => new FormUrlEncodedContent(fields);

    static StringContent JsonBody(object payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    /// <summary>
    /// A client that does not follow redirects (so the OAuth authorize 302 is
    /// observed directly) but still handles cookies (so the session is kept).
    /// </summary>
    HttpClient CreateNoRedirectClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    /// <summary>
    /// Registers + logs in a user and returns an authenticated client (cookie
    /// session) plus the username.
    /// </summary>
    async Task<(HttpClient client, string username)> GetAuthenticatedClient()
    {
        var client = CreateNoRedirectClient();
        var username = $"ratelimit_{Guid.NewGuid().ToString("N")[..8]}";
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Rate Limit User" },
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

        return (client, username);
    }

    /// <summary>
    /// Runs the OAuth authorization-code flow to mint a Bearer access token for
    /// a newly registered application, and returns (token, clientId).
    /// </summary>
    async Task<(string accessToken, string clientId)> GetBearerToken()
    {
        // One no-redirect, cookie-handling client for the whole flow.
        var client = CreateNoRedirectClient();
        var username = $"ratelimit_bearer_{Guid.NewGuid().ToString("N")[..8]}";
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Rate Limit Bearer User" },
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

        // Register an application.
        var appResponse = await client.PostAsync("/api/v1/apps", JsonBody(new
        {
            name = "Rate Limit App",
            redirect_uris = "https://client.example.com/callback",
            scopes = "read",
        }));
        Assert.Equal(HttpStatusCode.OK, appResponse.StatusCode);
        var clientId = JsonDocument.Parse(await appResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("client_id").GetString()!;

        // Authorize (no PKCE for this flow) — 302 with ?code=...
        var authResponse = await client.GetAsync(
            $"/api/v1/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri=https://client.example.com/callback");
        Assert.Equal(HttpStatusCode.Redirect, authResponse.StatusCode);
        var location = authResponse.Headers.Location!.ToString();
        var code = ExtractCode(location);

        // Exchange the code for a bearer token.
        var tokenResponse = await client.PostAsync("/api/v1/oauth/token", CreateFormContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "client_id", clientId },
        }));
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var accessToken = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("access_token").GetString()!;

        return (accessToken, clientId);
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

    [Fact]
    public async Task ApiResponse_ContainsRateLimitHeaders()
    {
        var (client, _) = await GetAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/timelines/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(response.Headers.Contains("RateLimit-Limit"), "RateLimit-Limit header should be present");
        Assert.True(response.Headers.Contains("RateLimit-Remaining"), "RateLimit-Remaining header should be present");
        Assert.True(response.Headers.Contains("RateLimit-Reset"), "RateLimit-Reset header should be present");

        var limit = response.Headers.GetValues("RateLimit-Limit").Single();
        var remaining = response.Headers.GetValues("RateLimit-Remaining").Single();
        Assert.True(int.Parse(limit) > 0, "RateLimit-Limit should be positive");
        Assert.True(int.Parse(remaining) < int.Parse(limit), "Remaining should be less than limit after one request");
    }

    [Fact]
    public async Task CookieClient_ExceedingLimit_Returns429()
    {
        // Lower the limit for this test by mutating the singleton options
        // (the limiter reads options.Value on each call).
        var optionsMonitor = _factory.Services.GetRequiredService<IOptions<ApiRateLimitOptions>>();
        var originalMax = optionsMonitor.Value.MaxRequests;
        optionsMonitor.Value.MaxRequests = 3;

        try
        {
            var (client, _) = await GetAuthenticatedClient();

            // 3 allowed, then 429.
            for (int i = 0; i < 3; i++)
            {
                var ok = await client.GetAsync("/api/v1/timelines/home");
                Assert.True(ok.StatusCode == HttpStatusCode.OK,
                    $"Request {i + 1} of 3 should succeed, got {(int)ok.StatusCode}");
            }

            var limited = await client.GetAsync("/api/v1/timelines/home");
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.True(limited.Headers.Contains("RateLimit-Remaining"));
            Assert.Equal("0", limited.Headers.GetValues("RateLimit-Remaining").Single());
        }
        finally
        {
            optionsMonitor.Value.MaxRequests = originalMax;
        }
    }

    [Fact]
    public async Task BearerToken_ExceedingPerAppLimit_Returns429()
    {
        var (accessToken, clientId) = await GetBearerToken();

        var optionsMonitor = _factory.Services.GetRequiredService<IOptions<ApiRateLimitOptions>>();
        var policy = new ApiRateLimitPolicy { MaxRequests = 2 };
        optionsMonitor.Value.PerApplication[clientId] = policy;

        try
        {
            var api = CreateClient();
            api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var first = await api.GetAsync("/api/v1/timelines/home");
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal("2", first.Headers.GetValues("RateLimit-Limit").Single());

            var second = await api.GetAsync("/api/v1/timelines/home");
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);

            var third = await api.GetAsync("/api/v1/timelines/home");
            Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
            Assert.Equal("0", third.Headers.GetValues("RateLimit-Remaining").Single());
        }
        finally
        {
            optionsMonitor.Value.PerApplication.TryRemove(clientId, out _);
        }
    }

    [Fact]
    public async Task NonApiPath_IsNotRateLimited()
    {
        // The public home page is not under /api/v1, so no RateLimit headers.
        var client = CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(!response.Headers.Contains("RateLimit-Limit"),
            "Non-API paths must not carry API rate-limit headers");
    }
}
