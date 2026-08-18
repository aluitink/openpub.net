using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class ReplyComposeTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ReplyComposeTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    async Task<HttpClient> GetAuthenticatedClient(string? prefix = null)
    {
        var client = _factory.CreateClient();
        var username = $"{prefix ?? "replyc"}_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Reply Test" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        return client;
    }

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
    {
        return new FormUrlEncodedContent(fields);
    }

    static string? ExtractActivityId(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(html, @"name=""activityId""\s+value=""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    [Fact]
    public async Task Compose_WithReplyToShowsReplyBannerAndHiddenField()
    {
        var client = await GetAuthenticatedClient();
        var unique = $"replytarget_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", unique },
        }));

        var timeline = await client.GetAsync("/timeline");
        var timelineBody = await timeline.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var response = await client.GetAsync($"/compose?replyTo={Uri.EscapeDataString(activityId!)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("reply-context-banner", body);
        Assert.Contains("Replying to", body);
        Assert.Contains("name=\"InReplyTo\" value=\"" + activityId + "\"", body);
        Assert.Contains("Cancel reply", body);
    }

    [Fact]
    public async Task Compose_WithUnknownReplyToShowsPlainCompose()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/compose?replyTo=https://localhost/users/nobody/activities/doesnotexist");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("reply-context-banner", body);
        Assert.Contains("Compose New Post", body);
    }

    [Fact]
    public async Task Compose_WithoutReplyToHasNoBanner()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/compose");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("reply-context-banner", body);
        Assert.Contains("Compose New Post", body);
    }

    [Fact]
    public async Task Timeline_NoteCard_MoreMenu_HasReplyViaComposeLink()
    {
        var client = await GetAuthenticatedClient();
        var unique = $"menucheck_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", unique },
        }));

        var response = await client.GetAsync("/timeline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(body);
        Assert.NotNull(activityId);
        Assert.Contains("Reply via Compose", body);
        Assert.Contains("replyTo=", body);
    }

    [Fact]
    public async Task Post_WithInReplyTo_CreatesReplyNote()
    {
        var client = await GetAuthenticatedClient();
        var uniqueTarget = $"repliesrc_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", uniqueTarget },
        }));

        var timeline = await client.GetAsync("/timeline");
        var timelineBody = await timeline.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var replyContent = $"replybody_{Guid.NewGuid().ToString("N")[..8]}";
        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", replyContent },
            { "InReplyTo", activityId! },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null,
            $"Reply post failed: {(int)postResponse.StatusCode}");

        var newTimeline = await client.GetAsync("/timeline");
        var newBody = await newTimeline.Content.ReadAsStringAsync();
        Assert.Contains(replyContent, newBody);
        Assert.Contains("reply-indicator", newBody);
    }

    [Fact]
    public async Task Post_ReplyIncrementsReplyCountOnTarget()
    {
        var client = await GetAuthenticatedClient();
        var uniqueTarget = $"replycount_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", uniqueTarget },
        }));

        var timeline = await client.GetAsync("/timeline");
        var timelineBody = await timeline.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", $"a-reply-{Guid.NewGuid():N}" },
            { "InReplyTo", activityId! },
        }));

        var newTimeline = await client.GetAsync("/timeline");
        var newBody = await newTimeline.Content.ReadAsStringAsync();
        // Phase 49.3: reply count follows the inline-SVG icon, not a glyph.
        var m = Regex.Match(newBody, @"btn-reply\b[^>]*>\s*<span[^>]*class=""fb-icon""[\s\S]*?</span>\s*1\b");
        Assert.True(m.Success, "reply count 1 not found on target note's reply button");
    }

    [Fact]
    public async Task Post_ReplyToOwnNote_DoesNotFail()
    {
        var client = await GetAuthenticatedClient();
        var uniqueTarget = $"ownreply_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", uniqueTarget },
        }));

        var timeline = await client.GetAsync("/timeline");
        var timelineBody = await timeline.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", $"self-reply-{Guid.NewGuid():N}" },
            { "InReplyTo", activityId! },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null,
            $"Self-reply post failed: {(int)postResponse.StatusCode}");
    }
}
