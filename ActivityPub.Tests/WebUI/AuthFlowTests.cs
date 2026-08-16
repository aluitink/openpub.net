using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class AuthFlowTests : IClassFixture<WebUIFactory>
{
    private readonly HttpClient _client;

    public AuthFlowTests(WebUIFactory factory)
    {
        _client = factory.CreateClient();

        // Ensure databases are created
        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    [Fact]
    public async Task RegisterPage_Returns200()
    {
        var response = await _client.GetAsync("/auth/register");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LoginPage_Returns200()
    {
        var response = await _client.GetAsync("/auth/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HomePage_Returns200()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterWithValidData_SignsInUserAndRedirects()
    {
        var username = $"testuser_{Guid.NewGuid().ToString("N")[..8]}";
        var response = await _client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Test User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));

        if (!response.IsSuccessStatusCode && response.Headers.Location == null)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Got {(int)response.StatusCode}: {body.Substring(0, Math.Min(200, body.Length))}");
        }

        Assert.True(response.IsSuccessStatusCode || response.Headers.Location != null,
            $"Expected success or redirect, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task RegisterWithDuplicateUsername_ReturnsValidationError()
    {
        var username = $"dupuser_{Guid.NewGuid().ToString("N")[..8]}";

        var response1 = await _client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}1@test.com" },
            { "DisplayName", "Test User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));

        var response2 = await _client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}2@test.com" },
            { "DisplayName", "Test User 2" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));

        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var body = await response2.Content.ReadAsStringAsync();
        Assert.Contains("already taken", body);
    }

    [Fact]
    public async Task RegisterWithMismatchedPasswords_ReturnsValidationError()
    {
        var username = $"mismatch_{Guid.NewGuid().ToString("N")[..8]}";
        var response = await _client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", "mismatch@test.com" },
            { "DisplayName", "Mismatch User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Different123!" },
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("do not match", body);
    }

    [Fact]
    public async Task RegisterWithShortUsername_ReturnsValidationError()
    {
        var response = await _client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", "ab" },
            { "Email", "short@test.com" },
            { "DisplayName", "Short User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("3-30 characters", body);
    }

    [Fact]
    public async Task LoginWithInvalidCredentials_ReturnsError()
    {
        var response = await _client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", "nonexistent" },
            { "Password", "wrongpassword" },
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid login attempt", body);
    }

    [Fact]
    public async Task RegisterAndLogin_FullFlow()
    {
        var username = $"fullflow_{Guid.NewGuid().ToString("N")[..8]}";

        var registerResponse = await _client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Full Flow User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(registerResponse.IsSuccessStatusCode || registerResponse.Headers.Location != null);

        var loginResponse = await _client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        Assert.True(loginResponse.IsSuccessStatusCode);

        var homeResponse = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        var homeBody = await homeResponse.Content.ReadAsStringAsync();
        Assert.Contains(username, homeBody);
    }

    private static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
    {
        return new FormUrlEncodedContent(fields);
    }
}

public class WebUIFactory : WebApplicationFactory<ActivityPub.WebUI.Program>
{
    private readonly string _dbId = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(s => s.ServiceType.IsGenericType &&
                            s.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
                .ToList();
            foreach (var s in toRemove)
                services.Remove(s);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source=/tmp/test_identity_{_dbId}.db"));

            services.AddDbContext<ActivityPubDbContext>(options =>
                options.UseSqlite($"Data Source=/tmp/test_ap_{_dbId}.db"));

            services.AddSingleton<IAntiforgery, PermissiveAntiforgery>();
        });
    }
}

public class PermissiveAntiforgery : IAntiforgery
{
    public Task<bool> IsRequestValidAsync(HttpContext context) => Task.FromResult(true);

    public Task ValidateRequestAsync(HttpContext context) => Task.CompletedTask;

    public void SetCookieTokenAndHeader(HttpContext context) { }

    public AntiforgeryTokenSet GetTokens(HttpContext context)
        => new AntiforgeryTokenSet(requestToken: "", cookieToken: "", formFieldName: "__RequestVerificationToken", headerName: null);

    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext context)
        => new AntiforgeryTokenSet(requestToken: "", cookieToken: "", formFieldName: "__RequestVerificationToken", headerName: null);
}
