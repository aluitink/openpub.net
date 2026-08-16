using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class InteractionTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public InteractionTests(WebUIFactory factory)
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
            { "DisplayName", "Test User" },
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
        var username = $"it_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);
        return client;
    }

    [Fact]
    public async Task Like_RequiresAuthentication()
    {
        var client = CreateClient();
        var response = await client.PostAsync("/interaction/like", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", "test" },
        }));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Login", body);
    }

    [Fact]
    public async Task Like_CreatesLikeActivity()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Like me!" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var likeResponse = await client.PostAsync("/interaction/like", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));
        Assert.True(likeResponse.IsSuccessStatusCode || likeResponse.Headers.Location != null,
            $"Like failed: {(int)likeResponse.StatusCode}");

        var afterLike = await client.GetAsync("/timeline");
        var afterLikeBody = await afterLike.Content.ReadAsStringAsync();
        Assert.Contains("♥ 1", afterLikeBody);
    }

    [Fact]
    public async Task LikeSamePostTwice_ReturnsError()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Double like test" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        await client.PostAsync("/interaction/like", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));

        var secondLike = await client.PostAsync("/interaction/like", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));

        var secondLikeBody = await secondLike.Content.ReadAsStringAsync();
        Assert.Contains("already liked", secondLikeBody);
    }

    [Fact]
    public async Task Unlike_RemovesLike()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Unlike test" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        await client.PostAsync("/interaction/like", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));

        var afterLike = await client.GetAsync("/timeline");
        Assert.Contains("♥ 1", await afterLike.Content.ReadAsStringAsync());

        await client.PostAsync("/interaction/unlike", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));

        var afterUnlike = await client.GetAsync("/timeline");
        var afterUnlikeBody = await afterUnlike.Content.ReadAsStringAsync();
        Assert.Contains("♥ 0", afterUnlikeBody);
    }

    [Fact]
    public async Task Boost_CreatesAnnounceActivity()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Boost me!" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var boostResponse = await client.PostAsync("/interaction/boost", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));
        Assert.True(boostResponse.IsSuccessStatusCode || boostResponse.Headers.Location != null,
            $"Boost failed: {(int)boostResponse.StatusCode}");

        var afterBoost = await client.GetAsync("/timeline");
        var afterBoostBody = await afterBoost.Content.ReadAsStringAsync();
        Assert.Contains("↻ 1", afterBoostBody);
    }

    [Fact]
    public async Task BoostSamePostTwice_ReturnsError()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Double boost test" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        await client.PostAsync("/interaction/boost", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));

        var secondBoost = await client.PostAsync("/interaction/boost", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));

        var secondBoostBody = await secondBoost.Content.ReadAsStringAsync();
        Assert.Contains("already boosted", secondBoostBody);
    }

    [Fact]
    public async Task Unboost_RemovesBoost()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Unboost test" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        await client.PostAsync("/interaction/boost", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));

        await client.PostAsync("/interaction/unboost", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));

        var afterUnboost = await client.GetAsync("/timeline");
        var afterUnboostBody = await afterUnboost.Content.ReadAsStringAsync();
        Assert.Contains("↻ 0", afterUnboostBody);
    }

    [Fact]
    public async Task Reply_CreatesReplyNote()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Reply to this" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var replyResponse = await client.PostAsync("/interaction/reply", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
            { "content", "This is my reply" },
        }));
        Assert.True(replyResponse.IsSuccessStatusCode || replyResponse.Headers.Location != null,
            $"Reply failed: {(int)replyResponse.StatusCode}");

        var afterReply = await client.GetAsync("/timeline");
        var afterReplyBody = await afterReply.Content.ReadAsStringAsync();
        Assert.Contains("This is my reply", afterReplyBody);
        Assert.Contains("Reply", afterReplyBody);
        Assert.Contains("💬 1", afterReplyBody);
    }

    [Fact]
    public async Task ReplyWithEmptyContent_ReturnsError()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Empty reply test" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var replyResponse = await client.PostAsync("/interaction/reply", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
            { "content", "" },
        }));

        var replyBody = await replyResponse.Content.ReadAsStringAsync();
        Assert.Contains("1 and 500", replyBody);
    }

    [Fact]
    public async Task ReplyWithLongContent_ReturnsError()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Long reply test" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var replyResponse = await client.PostAsync("/interaction/reply", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
            { "content", new string('x', 501) },
        }));

        var replyBody = await replyResponse.Content.ReadAsStringAsync();
        Assert.Contains("1 and 500", replyBody);
    }

    [Fact]
    public async Task Timeline_ShowsInteractionButtons()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Buttons test" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        Assert.Contains("btn-like", timelineBody);
        Assert.Contains("btn-boost", timelineBody);
        Assert.Contains("btn-reply", timelineBody);
    }

    [Fact]
    public async Task MultipleUsers_CanLikeSamePost()
    {
        var authorClient = CreateClient();
        var authorName = $"author_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(authorClient, authorName);
        await LoginUser(authorClient, authorName);

        var postResponse = await authorClient.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Multi like test" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await authorClient.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var likerClient = CreateClient();
        var likerName = $"liker_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(likerClient, likerName);
        await LoginUser(likerClient, likerName);

        var likeResponse = await likerClient.PostAsync("/interaction/like", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));
        Assert.True(likeResponse.IsSuccessStatusCode || likeResponse.Headers.Location != null,
            $"Like failed: {(int)likeResponse.StatusCode}");
    }

    [Fact]
    public async Task LikeAndBoost_BothWork()
    {
        var client = await GetAuthenticatedClient();

        var postResponse = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Like and boost test" },
        }));
        Assert.True(postResponse.IsSuccessStatusCode || postResponse.Headers.Location != null);

        var timelineResponse = await client.GetAsync("/timeline");
        var timelineBody = await timelineResponse.Content.ReadAsStringAsync();
        var activityId = ExtractActivityId(timelineBody);
        Assert.NotNull(activityId);

        var likeResponse = await client.PostAsync("/interaction/like", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));
        Assert.True(likeResponse.IsSuccessStatusCode || likeResponse.Headers.Location != null);

        var boostResponse = await client.PostAsync("/interaction/boost", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));
        Assert.True(boostResponse.IsSuccessStatusCode || boostResponse.Headers.Location != null);

        var afterBoth = await client.GetAsync("/timeline");
        var afterBothBody = await afterBoth.Content.ReadAsStringAsync();
        Assert.Contains("♥ 1", afterBothBody);
        Assert.Contains("↻ 1", afterBothBody);
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
