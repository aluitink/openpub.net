using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 52.0 — WebFinger validation (remote→local). Verifies that remote
/// ActivityPub instances can resolve a local user end-to-end:
///
///   GET /.well-known/webfinger?resource=acct:{user}@{host}
///     → 200 JRD whose `self` link points at the actor document
///   GET {host}/users/{user}   (the actor document the self link targets)
///     → 200 Person whose id/url/inbox/outbox/followers/following/liked all
///       use the REAL request host (not a hard-coded https://localhost that
///       remote instances cannot resolve).
///
/// This was the "user not found from remote" blocker: the actor document's
/// federation URLs were hard-coded to https://localhost, so even when a remote
/// instance followed webfinger → actor doc, every URL was unusable.
/// </summary>
public class WebFingerFederationTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public WebFingerFederationTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields) =>
        new FormUrlEncodedContent(fields);

    async Task RegisterUser(HttpClient client, string username, string displayName = "Test User")
    {
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", displayName },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(registerResponse.IsSuccessStatusCode || registerResponse.Headers.Location != null,
            $"Register failed: {(int)registerResponse.StatusCode}");
    }

    // The test server's host is "localhost" (http scheme), so every URL the
    // endpoints emit should start with http://localhost — the request host —
    // and never a hard-coded https://localhost that would be wrong in prod.
    const string ExpectedHost = "http://localhost";

    [Fact]
    public async Task WebFinger_ResolvesLocalUser_ReturnsJrdWithSelfLink()
    {
        var client = CreateClient();
        var username = $"wf_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);

        var resource = $"acct:{username}@localhost";
        var response = await client.GetAsync($"/.well-known/webfinger?resource={Uri.EscapeDataString(resource)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/jrd+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(resource, body);
        Assert.Contains("\"self\"", body);
        Assert.Contains("application/activity+json", body);
        // The self href must target the actor document on the real request host.
        Assert.Contains($"{ExpectedHost}/users/{username}", body);
        Assert.DoesNotContain("https://localhost", body);
    }

    [Fact]
    public async Task ActorDocument_FromWebFingerSelfLink_UsesRequestHost()
    {
        // Follow the exact path a remote instance takes: webfinger → self href
        // → actor document. The document's federation URLs must be valid for
        // the deployed domain, not a hard-coded localhost.
        var client = CreateClient();
        var username = $"af_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);

        var wf = await client.GetAsync(
            $"/.well-known/webfinger?resource={Uri.EscapeDataString($"acct:{username}@localhost")}");
        var wfBody = await wf.Content.ReadAsStringAsync();
        Assert.Contains($"{ExpectedHost}/users/{username}", wfBody);

        var response = await client.GetAsync($"/users/{username}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/activity+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Person", body);
        // id + every collection/inbox URL must derive from the request host.
        Assert.Contains($"{ExpectedHost}/users/{username}", body);
        Assert.Contains($"{ExpectedHost}/users/{username}/inbox", body);
        Assert.Contains($"{ExpectedHost}/users/{username}/outbox", body);
        Assert.Contains($"{ExpectedHost}/users/{username}/followers", body);
        Assert.Contains($"{ExpectedHost}/users/{username}/following", body);
        Assert.Contains($"{ExpectedHost}/users/{username}/liked", body);
        // id + publicKey owner must be the same canonical actor id.
        Assert.Contains($"\"id\":\"{ExpectedHost}/users/{username}\"", body.Replace(" ", ""));
    }

    [Fact]
    public async Task ActorDocument_Returns404_ForUnknownUser_ButWebFingerIsLenient()
    {
        // The Core WebFinger endpoint is intentionally lenient: for any
        // well-formed acct: URI on the local domain it returns a 200 JRD (the
        // self link is computed from the handle, not from a DB lookup). What
        // must NOT be 200 is the actor document itself — a remote instance
        // that follows webfinger → self for a non-existent user gets a clean
        // 404 from the actor endpoint, not a bogus Person.
        var client = CreateClient();
        var ghost = "does_not_exist_99999";

        var wf = await client.GetAsync(
            $"/.well-known/webfinger?resource={Uri.EscapeDataString($"acct:{ghost}@localhost")}");
        Assert.Equal(HttpStatusCode.OK, wf.StatusCode);

        var actor = await client.GetAsync($"/users/{ghost}");
        Assert.Equal(HttpStatusCode.NotFound, actor.StatusCode);
    }

    [Fact]
    public async Task WebFinger_Returns400_WhenResourceMissing()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/.well-known/webfinger");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ActorDocument_WebUICollection_RoutesUseRequestHost()
    {
        // The WebUI's own actor/collection routes (/actors/...) must also
        // derive URLs from the request host so either entry point a remote
        // instance may use yields resolvable URLs.
        var client = CreateClient();
        var username = $"wc_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);

        var show = await client.GetAsync($"/actors/show/{username}");
        Assert.Equal(HttpStatusCode.OK, show.StatusCode);
        var showBody = await show.Content.ReadAsStringAsync();
        Assert.Contains($"{ExpectedHost}/actors/show/{username}", showBody);
        Assert.Contains($"{ExpectedHost}/inbox/{username}", showBody);

        var outbox = await client.GetAsync($"/actors/outbox/{username}");
        Assert.Equal(HttpStatusCode.OK, outbox.StatusCode);
        var outboxBody = await outbox.Content.ReadAsStringAsync();
        Assert.Contains($"{ExpectedHost}/actors/outbox/{username}", outboxBody);
    }

    [Fact]
    public async Task ActorDocument_Returns404ForUnknownUser()
    {
        var client = CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/users/nonexistent_user_12345")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/actors/show/nonexistent_user_12345")).StatusCode);
    }
}
