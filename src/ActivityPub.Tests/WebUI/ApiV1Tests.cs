using System.Net;
using System.Text.Json;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Tests for the local Mastodon-compatible REST API under /api/v1
/// (statuses, accounts, timelines).
/// </summary>
public class ApiV1Tests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ApiV1Tests(WebUIFactory factory)
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
        var username = $"api_{Guid.NewGuid().ToString("N")[..8]}";
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "API Test User" },
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

    async Task<string> RegisterUserAndGetUsername()
    {
        var client = CreateClient();
        var username = $"api_{Guid.NewGuid().ToString("N")[..8]}";
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "API Test User" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(registerResponse.IsSuccessStatusCode || registerResponse.Headers.Location != null,
            $"Register failed: {(int)registerResponse.StatusCode}");
        return username;
    }

    async Task PostNote(HttpClient client)
    {
        var response = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Hello from the API test!" },
        }));
        Assert.True(response.IsSuccessStatusCode || response.Headers.Location != null,
            $"Post failed: {(int)response.StatusCode}");
    }

    async Task<HttpClient> GetAuthenticatedClientFor(string username)
    {
        var client = CreateClient();
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "API Test User" },
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

    // ---------- Accounts ----------

    [Fact]
    public async Task Accounts_Unknown_Returns404()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/v1/accounts/doesnotexist_zzz");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Accounts_Existing_ReturnsMastodonAccount()
    {
        var username = await RegisterUserAndGetUsername();
        var client = CreateClient();
        var response = await client.GetAsync($"/api/v1/accounts/{username}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(username, root.GetProperty("username").GetString());
        Assert.Equal(username, root.GetProperty("acct").GetString());
        Assert.True(root.TryGetProperty("display_name", out _));
        Assert.True(root.TryGetProperty("followers_count", out _));
        Assert.True(root.TryGetProperty("following_count", out _));
        Assert.True(root.TryGetProperty("statuses_count", out _));
    }

    [Fact]
    public async Task Accounts_Lookup_ReturnsAccount()
    {
        var username = await RegisterUserAndGetUsername();
        var client = CreateClient();
        var response = await client.GetAsync($"/api/v1/accounts?acct={username}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(username, doc.RootElement.GetProperty("username").GetString());
    }

    // ---------- Statuses ----------

    [Fact]
    public async Task Statuses_Unknown_Returns404()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/v1/statuses/00000000-0000-0000-0000-000000000000");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Statuses_WithoutAccount_Returns400()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/v1/statuses");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Statuses_ByAccount_ReturnsNotes()
    {
        var username = $"api_{Guid.NewGuid().ToString("N")[..8]}";
        var client = await GetAuthenticatedClientFor(username);
        await PostNote(client);

        var response = await client.GetAsync($"/api/v1/statuses?account={username}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var array = doc.RootElement;
        Assert.True(array.GetArrayLength() > 0, "Expected at least one status for the account");

        var first = array[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("content", out _));
        Assert.True(first.TryGetProperty("account", out _));
    }

    [Fact]
    public async Task AccountStatuses_ReturnsMastodonStatus()
    {
        var client = await GetAuthenticatedClient();
        await PostNote(client);

        // The authenticated user's username is embedded in the timeline; instead,
        // we fetch by resolving the author from the home timeline.
        var homeResponse = await client.GetAsync("/api/v1/timelines/home");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);

        var homeJson = await homeResponse.Content.ReadAsStringAsync();
        using var homeDoc = JsonDocument.Parse(homeJson);
        Assert.True(homeDoc.RootElement.GetArrayLength() > 0, "Expected at least one status in home timeline");

        var first = homeDoc.RootElement[0];
        var accountEl = first.GetProperty("account");
        var accountId = accountEl.GetProperty("id").GetString()!;
        var accountUsername = accountEl.GetProperty("username").GetString()!;
        var statusId = first.GetProperty("id").GetString()!;

        var statusResponse = await client.GetAsync($"/api/v1/statuses/{statusId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var statusJson = await statusResponse.Content.ReadAsStringAsync();
        using var statusDoc = JsonDocument.Parse(statusJson);
        var root = statusDoc.RootElement;

        Assert.Equal(statusId, root.GetProperty("id").GetString());
        Assert.True(root.TryGetProperty("content", out _));
        Assert.True(root.TryGetProperty("created_at", out _));
        Assert.True(root.TryGetProperty("favourites_count", out _));
        Assert.True(root.TryGetProperty("reblogs_count", out _));
        Assert.True(root.TryGetProperty("account", out _));
        Assert.True(root.TryGetProperty("media_attachments", out _));

        // Account's statuses endpoint returns the same note.
        var accStatusesResponse = await client.GetAsync($"/api/v1/accounts/{accountUsername}/statuses");
        Assert.Equal(HttpStatusCode.OK, accStatusesResponse.StatusCode);
        var accJson = await accStatusesResponse.Content.ReadAsStringAsync();
        using var accDoc = JsonDocument.Parse(accJson);
        Assert.True(accDoc.RootElement.GetArrayLength() > 0);
    }

    // ---------- Timelines ----------

    [Fact]
    public async Task HomeTimeline_RequiresAuthentication()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/v1/timelines/home");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HomeTimeline_ReturnsAuthedUsersNotes()
    {
        var client = await GetAuthenticatedClient();
        await PostNote(client);

        var response = await client.GetAsync("/api/v1/timelines/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var array = doc.RootElement;
        Assert.True(array.GetArrayLength() > 0, "Expected at least one status in the home timeline");

        var first = array[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("content", out _));
        Assert.True(first.TryGetProperty("account", out _));
        Assert.True(first.TryGetProperty("created_at", out _));
    }

    [Fact]
    public async Task DeleteStatus_RemovesNote()
    {
        var client = await GetAuthenticatedClient();
        await PostNote(client);

        var homeResponse = await client.GetAsync("/api/v1/timelines/home");
        var homeJson = await homeResponse.Content.ReadAsStringAsync();
        using var homeDoc = JsonDocument.Parse(homeJson);
        var statusId = homeDoc.RootElement[0].GetProperty("id").GetString()!;

        var deleteResponse = await client.DeleteAsync($"/api/v1/statuses/{statusId}");
        Assert.True(deleteResponse.StatusCode == HttpStatusCode.NoContent || deleteResponse.IsSuccessStatusCode,
            $"Delete failed: {(int)deleteResponse.StatusCode}");

        var afterResponse = await client.GetAsync($"/api/v1/statuses/{statusId}");
        Assert.Equal(HttpStatusCode.NotFound, afterResponse.StatusCode);
    }
}
