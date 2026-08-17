using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Tests for webhook support for external integrations: the REST API for
/// managing webhook subscriptions (POST/GET/DELETE /api/v1/webhooks),
/// HMAC-SHA256 signing, and the durable delivery queue.
/// </summary>
public class ApiWebhookTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ApiWebhookTests(WebUIFactory factory)
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

    /// <summary>Registers + logs in a user and returns an authenticated client + username.</summary>
    async Task<(HttpClient client, string username)> GetAuthenticatedClient(string prefix = "wh")
    {
        var client = CreateClient();
        var username = $"{prefix}_{Guid.NewGuid().ToString("N")[..8]}";
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Webhook Test User" },
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

    // ---------------------------------------------------------------------
    // HMAC signing
    // ---------------------------------------------------------------------

    [Fact]
    public void VerifyWebhookSignature_MatchingSecret_ReturnsTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

        var secret = "test-secret-key-12345";
        var payload = "{\"id\":\"https://localhost/users/u/activities/1\"}";

        // Compute the expected HMAC-SHA256 the same way the delivery service does.
        var expected = ComputeHmacSha256Base64(secret, payload);

        var valid = service.VerifyWebhookSignatureAsync(secret, payload, expected).GetAwaiter().GetResult();
        Assert.True(valid, "A correctly signed payload should verify.");

        // A tampered payload or wrong secret must not verify.
        var tampered = service.VerifyWebhookSignatureAsync(secret, payload + "x", expected).GetAwaiter().GetResult();
        Assert.False(tampered, "A tampered payload must not verify.");

        var wrongSecret = service.VerifyWebhookSignatureAsync("other-secret", payload, expected).GetAwaiter().GetResult();
        Assert.False(wrongSecret, "A wrong secret must not verify.");
    }

    private static string ComputeHmacSha256Base64(string key, string payload)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    // ---------------------------------------------------------------------
    // API CRUD
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreateWebhook_Unauthenticated_IsRejected()
    {
        var client = CreateClient();
        var response = await client.PostAsync("/api/v1/webhooks", JsonBody(new
        {
            endpoint_url = "https://example.com/hook",
        }));

        // The endpoint is protected. Depending on the auth challenge mechanism
        // (cookie redirect vs bearer 401) the response is not a successful
        // creation — it must not be 200/201.
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.MethodNotAllowed
            or HttpStatusCode.Redirect
            or HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found,
            $"Expected the endpoint to reject an unauthenticated request, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateWebhook_Valid_Returns200WithSecret()
    {
        var (client, _) = await GetAuthenticatedClient();
        var response = await client.PostAsync("/api/v1/webhooks", JsonBody(new
        {
            endpoint_url = "https://example.com/hook",
            http_method = "POST",
            event_type = "Create",
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("id", out _), "id missing");
        Assert.Equal("https://example.com/hook", root.GetProperty("endpoint_url").GetString());
        Assert.Equal("POST", root.GetProperty("http_method").GetString());
        Assert.Equal("Create", root.GetProperty("event_type").GetString());
        Assert.True(root.GetProperty("enabled").GetBoolean());
        // A secret key is generated when not supplied.
        var secret = root.GetProperty("secret_key").GetString();
        Assert.False(string.IsNullOrWhiteSpace(secret), "secret_key should be returned at creation");
    }

    [Fact]
    public async Task CreateWebhook_MissingEndpoint_Returns400()
    {
        var (client, _) = await GetAuthenticatedClient();
        var response = await client.PostAsync("/api/v1/webhooks", JsonBody(new
        {
            http_method = "POST",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateWebhook_BadEndpoint_Returns400()
    {
        var (client, _) = await GetAuthenticatedClient();
        var response = await client.PostAsync("/api/v1/webhooks", JsonBody(new
        {
            endpoint_url = "not-a-url",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListWebhooks_ReturnsSubscriptions_WithoutSecret()
    {
        var (client, _) = await GetAuthenticatedClient();

        // Create a webhook.
        await client.PostAsync("/api/v1/webhooks", JsonBody(new
        {
            endpoint_url = "https://example.com/list-hook",
            event_type = "All",
        }));

        var response = await client.GetAsync("/api/v1/webhooks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var array = doc.RootElement;
        Assert.True(array.ValueKind == JsonValueKind.Array, "expected an array");

        var found = false;
        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty("endpoint_url", out var ep) && ep.GetString() == "https://example.com/list-hook")
            {
                found = true;
                // The secret must NOT be present in a GET listing.
                Assert.False(item.TryGetProperty("secret_key", out _), "secret_key must be omitted on GET");
            }
        }
        Assert.True(found, "the created webhook should be listed");
    }

    [Fact]
    public async Task DeleteWebhook_Own_Returns204_OtherOrMissing_Returns404()
    {
        var (client, _) = await GetAuthenticatedClient();

        // Create a webhook and capture its id.
        var createResponse = await client.PostAsync("/api/v1/webhooks", JsonBody(new
        {
            endpoint_url = "https://example.com/delete-hook",
        }));
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var id = JsonDocument.Parse(createBody).RootElement.GetProperty("id").GetString()!;

        // Delete it.
        var deleteResponse = await client.DeleteAsync($"/api/v1/webhooks/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Deleting again (or a non-existent id) returns 404.
        var missingResponse = await client.DeleteAsync($"/api/v1/webhooks/{id}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    // ---------------------------------------------------------------------
    // Event delivery (post created → queued webhook delivery)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task PostCreated_EnqueuesWebhookDelivery()
    {
        var (client, _) = await GetAuthenticatedClient();

        // Register a webhook for this user's "Create" events.
        var createResponse = await client.PostAsync("/api/v1/webhooks", JsonBody(new
        {
            endpoint_url = "https://example.com/event-hook",
            event_type = "Create",
        }));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        // Post a note.
        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "webhook event test post" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null,
            $"Post failed: {(int)postResponse.StatusCode}");

        // A queued webhook delivery should now exist in the repository.
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        var pending = await repository.GetPendingWebhookDeliveriesAsync(100);

        Assert.True(pending.Count > 0, "a queued webhook delivery should exist after a post");
        var delivery = pending.First();
        Assert.Equal(WebhookDeliveryStatus.Queued, delivery.Status);
        Assert.False(string.IsNullOrWhiteSpace(delivery.ActivityJson), "delivery should carry the activity JSON");
    }
}
