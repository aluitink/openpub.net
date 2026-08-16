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
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class ComposeTimelineTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ComposeTimelineTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task RegisterUser(HttpClient client, string username)
    {
        var registerResponse = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Test" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(registerResponse.IsSuccessStatusCode || registerResponse.Headers.Location != null,
            $"Register failed: {(int)registerResponse.StatusCode}");
    }

    async Task LoginUser(HttpClient client, string username)
    {
        var loginResponse = await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        Assert.True(loginResponse.IsSuccessStatusCode || loginResponse.Headers.Location != null,
            $"Login failed: {(int)loginResponse.StatusCode}");
    }

    async Task<HttpClient> GetAuthenticatedClient()
    {
        var client = CreateClient();
        var username = $"ct_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);
        return client;
    }

    [Fact]
    public async Task ComposePage_RequiresAuthentication()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/compose");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Login", body);
    }

    [Fact]
    public async Task TimelinePage_RequiresAuthentication()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/timeline");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Login", body);
    }

    [Fact]
    public async Task ComposePage_Returns200_WhenAuthenticated()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/compose");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TimelinePage_Returns200_WhenAuthenticated()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/timeline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostWithValidContent_CreatesNoteAndRedirects()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Hello fediverse!" },
        }));

        Assert.True(response.IsSuccessStatusCode || response.Headers.Location != null,
            $"Post failed: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task PostWithEmptyContent_ReturnsValidationError()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "" },
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("1 and 500", body);
    }

    [Fact]
    public async Task PostWithLongContent_ReturnsValidationError()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", new string('a', 501) },
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("1 and 500", body);
    }

    [Fact]
    public async Task Timeline_ShowsPostedNote()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Test timeline post" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        var body = await timelineResponse.Content.ReadAsStringAsync();
        Assert.Contains("Test timeline post", body);
    }

    [Fact]
    public async Task DeleteNote_RemovesFromTimeline()
    {
        var client = await GetAuthenticatedClient();
        var uniqueContent = $"Deletable_{Guid.NewGuid().ToString("N")[..8]}";

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", uniqueContent },
        }));

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        Assert.Contains(uniqueContent, timelineBody);

        var activityIds = ExtractAllActivityIds(timelineBody);
        Assert.NotEmpty(activityIds);

        var deleted = false;
        foreach (var aid in activityIds)
        {
            var deleteResponse = await client.PostAsync("/compose/delete", CreateFormContent(new Dictionary<string, string>
            {
                { "activityId", aid },
            }));
            if (deleteResponse.IsSuccessStatusCode || deleteResponse.Headers.Location != null)
                deleted = true;
        }
        Assert.True(deleted, "No activity could be deleted");

        var newTimelineResponse = await client.GetAsync("/timeline");
        var newTimelineBody = await newTimelineResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(uniqueContent, newTimelineBody);
    }

    static List<string> ExtractAllActivityIds(string html)
    {
        return System.Text.RegularExpressions.Regex.Matches(html, @"name=""activityId""\s+value=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    [Fact]
    public async Task ComposeAndTimeline_FullFlow()
    {
        var client = await GetAuthenticatedClient();

        var composeResponse = await client.GetAsync("/compose");
        Assert.Equal(HttpStatusCode.OK, composeResponse.StatusCode);
        var composeBody = await composeResponse.Content.ReadAsStringAsync();
        Assert.Contains("What's on your mind", composeBody);

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Full flow test post" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        Assert.Contains("Full flow test post", timelineBody);
    }

    static string? ExtractActivityId(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(html, @"name=""activityId""\s+value=""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
    {
        return new FormUrlEncodedContent(fields);
    }
}
