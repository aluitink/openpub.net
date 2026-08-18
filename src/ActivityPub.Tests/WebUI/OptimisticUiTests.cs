using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

/// <summary>
/// Phase 48.3 — Optimistic UI: server-authoritative reconciliation endpoints.
///
/// Covers the two new pieces of server support that the optimistic
/// follow/unfollow and like/boost toggles rely on:
///
///   1. GET /Profile/State — authoritative follow-state (isFollowing,
///      followerCount) so the client can reconcile its optimistic mutation
///      instead of trusting the local toggle.
///
///   2. GET /Timeline/Card/{id} — returns the server-rendered note-card
///      fragment. The like/boost client splices the fresh buttons back in
///      after a mutation. This also exercises the ExtractNote JsonElement
///      fix so boosted (Announce) notes render with real interaction counts
///      and the correct author, even when the activity is rehydrated from
///      stored JsonData.
/// </summary>
public class OptimisticUiTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public OptimisticUiTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

    async Task RegisterUser(HttpClient client, string username)
    {
        var response = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", username },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(response.IsSuccessStatusCode || response.Headers.Location != null,
            $"Register failed: {(int)response.StatusCode}");
    }

    async Task LoginUser(HttpClient client, string username)
    {
        var response = await client.PostAsync("/auth/login", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" },
        }));
        Assert.True(response.IsSuccessStatusCode || response.Headers.Location != null,
            $"Login failed: {(int)response.StatusCode}");
    }

    async Task<(HttpClient Client, string Username)> GetAuthClientWithUser()
    {
        var client = CreateClient();
        var username = $"opt_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);
        return (client, username);
    }

    // ------------------------------------------------------------------
    // Profile/State
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProfileState_OwnProfile_ReturnsIsOwnProfileAndZeroFollow()
    {
        var (client, username) = await GetAuthClientWithUser();

        var response = await client.GetAsync($"/Profile/State?username={username}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("isOwnProfile").GetBoolean());
        Assert.False(root.GetProperty("isFollowing").GetBoolean());
        Assert.Equal(0, root.GetProperty("followerCount").GetInt32());
    }

    [Fact]
    public async Task ProfileState_NotFollowing_ReturnsIsFollowingFalse()
    {
        var (follower, _) = await GetAuthClientWithUser();
        var (target, targetName) = await GetAuthClientWithUser();

        var response = await follower.GetAsync($"/Profile/State?username={targetName}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        var root = System.Text.Json.JsonDocument.Parse(body).RootElement;
        Assert.False(root.GetProperty("isOwnProfile").GetBoolean());
        Assert.False(root.GetProperty("isFollowing").GetBoolean());
        Assert.Equal(0, root.GetProperty("followerCount").GetInt32());
    }

    [Fact]
    public async Task ProfileState_AfterFollow_ReturnsIsFollowingTrueAndCountOne()
    {
        var (follower, followerName) = await GetAuthClientWithUser();
        var (target, targetName) = await GetAuthClientWithUser();

        // Follow via the /Follow page (the same backend path the profile
        // button hits) so the follow activity is persisted.
        var profileResp = await follower.GetAsync($"/Profile?username={targetName}");
        Assert.Equal(HttpStatusCode.OK, profileResp.StatusCode);
        var profileHtml = await profileResp.Content.ReadAsStringAsync();

        var actorIdMatch = Regex.Match(profileHtml, @"name=""actorId""[^>]*value=""([^""]+)""");
        Assert.True(actorIdMatch.Success, "actorId not found in profile page");
        var actorId = actorIdMatch.Groups[1].Value;

        var followResp = await follower.PostAsync("/Profile/Follow", CreateFormContent(new Dictionary<string, string>
        {
            { "actorId", actorId },
            { "returnUrl", $"/Profile?username={targetName}" },
        }));
        Assert.True(followResp.IsSuccessStatusCode || followResp.Headers.Location != null,
            $"Follow failed: {(int)followResp.StatusCode}");

        // The authoritative state endpoint must now report the follow.
        var stateResp = await follower.GetAsync($"/Profile/State?username={targetName}");
        Assert.Equal(HttpStatusCode.OK, stateResp.StatusCode);
        var stateBody = await stateResp.Content.ReadAsStringAsync();

        var root = System.Text.Json.JsonDocument.Parse(stateBody).RootElement;
        Assert.False(root.GetProperty("isOwnProfile").GetBoolean());
        Assert.True(root.GetProperty("isFollowing").GetBoolean(), $"Expected isFollowing=true, got: {stateBody}");
        Assert.True(root.GetProperty("followerCount").GetInt32() == 1, $"Expected followerCount=1, got: {stateBody}");
    }

    [Fact]
    public async Task ProfileState_AfterUnfollow_ReturnsIsFollowingFalse()
    {
        var (follower, _) = await GetAuthClientWithUser();
        var (target, targetName) = await GetAuthClientWithUser();

        // Follow first.
        var profileResp = await follower.GetAsync($"/Profile?username={targetName}");
        var profileHtml = await profileResp.Content.ReadAsStringAsync();
        var actorId = Regex.Match(profileHtml, @"name=""actorId""[^>]*value=""([^""]+)""").Groups[1].Value;

        await follower.PostAsync("/Profile/Follow", CreateFormContent(new Dictionary<string, string>
        {
            { "actorId", actorId },
        }));

        // Verify following.
        var state1 = await follower.GetAsync($"/Profile/State?username={targetName}");
        Assert.True(System.Text.Json.JsonDocument.Parse(await state1.Content.ReadAsStringAsync()).RootElement.GetProperty("isFollowing").GetBoolean());

        // Unfollow.
        await follower.PostAsync("/Profile/Unfollow", CreateFormContent(new Dictionary<string, string>
        {
            { "actorId", actorId },
        }));

        // Verify no longer following.
        var state2 = await follower.GetAsync($"/Profile/State?username={targetName}");
        var root = System.Text.Json.JsonDocument.Parse(await state2.Content.ReadAsStringAsync()).RootElement;
        Assert.False(root.GetProperty("isFollowing").GetBoolean());
        Assert.Equal(0, root.GetProperty("followerCount").GetInt32());
    }

    [Fact]
    public async Task ProfileState_DefaultsToOwnProfile_WhenNoUsername()
    {
        var (client, username) = await GetAuthClientWithUser();

        var response = await client.GetAsync("/Profile/State");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(root.GetProperty("isOwnProfile").GetBoolean());
        Assert.False(root.GetProperty("isFollowing").GetBoolean());
    }

    // ------------------------------------------------------------------
    // Timeline/Card — note fragment for reconciliation, including the
    // ExtractNote JsonElement fix (boosted/Announce notes render with
    // counts and the correct author).
    // ------------------------------------------------------------------

    [Fact]
    public async Task TimelineCard_ReturnsNoteFragment_WithActivityId()
    {
        var (client, username) = await GetAuthClientWithUser();

        // Post a note.
        var marker1 = "reconcile target note " + Guid.NewGuid().ToString("N")[..6];
        var postResp = await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", marker1 },
        }));
        Assert.True(postResp.IsSuccessStatusCode || postResp.Headers.Location != null,
            $"Post failed: {(int)postResp.StatusCode}");

        // Grab the activity id from the timeline page.
        var timelineResp = await client.GetAsync("/timeline");
        var timelineHtml = await timelineResp.Content.ReadAsStringAsync();
        var idMatch = Regex.Match(timelineHtml, @"data-activity-id=""([^""]+)""");
        Assert.True(idMatch.Success, "activity id not found on timeline");
        var activityId = idMatch.Groups[1].Value;

        var cardResp = await client.GetAsync("/timeline/card/" + Uri.EscapeDataString(activityId));
        Assert.Equal(HttpStatusCode.OK, cardResp.StatusCode);
        var cardHtml = await cardResp.Content.ReadAsStringAsync();

        Assert.Contains("note-card", cardHtml);
        Assert.Contains(activityId, cardHtml);
        Assert.Contains(marker1, cardHtml);
    }

    [Fact]
    public async Task TimelineCard_ForBoostedNote_RendersOriginalAuthorAndCounts()
    {
        // Alice posts a note; Bob boosts it; then Alice likes her own note.
        // Bob's timeline shows the boost (Announce) card. The card must
        // render the ORIGINAL note (author = Alice) with the like count,
        // which requires ExtractNote to deserialize the Announce's Object
        // from stored JsonData (a JsonElement), not fail the cast.
        var (alice, aliceName) = await GetAuthClientWithUser();
        var (bob, bobName) = await GetAuthClientWithUser();

        // Alice posts.
        var marker = "boosted note marker " + Guid.NewGuid().ToString("N")[..6];
        var postResp = await alice.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", marker },
        }));
        Assert.True(postResp.IsSuccessStatusCode || postResp.Headers.Location != null);

        // Find Alice's note activity id from her timeline.
        var aliceTimeline = await (await alice.GetAsync("/timeline")).Content.ReadAsStringAsync();
        var idMatch = Regex.Match(aliceTimeline, @"data-activity-id=""([^""]+)""");
        Assert.True(idMatch.Success, "Alice's note activity id not found");
        var activityId = idMatch.Groups[1].Value;

        // Alice likes her own note so the like count is non-zero.
        await alice.PostAsync("/interaction/like", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));

        // Bob boosts Alice's note (this also verifies the Announce is
        // created and addressed to the author's inbox).
        var boostResp = await bob.PostAsync("/interaction/boost", CreateFormContent(new Dictionary<string, string>
        {
            { "targetActivityId", activityId },
        }));
        Assert.True(boostResp.IsSuccessStatusCode || boostResp.Headers.Location != null,
            $"Boost failed: {(int)boostResp.StatusCode}");

        // The reconciliation endpoint the optimistic like/boost client calls
        // after a mutation is GET /timeline/card/{id}. Fetch it for the
        // original note and verify it renders the note text + the like count
        // + the boost count. This requires ExtractNote to deserialize the
        // activity's Object (stored as a JsonElement in JsonData) back into a
        // typed Note — the fix that made boosted/rehydrated notes render with
        // their real interaction counts.
        var cardResp = await alice.GetAsync("/timeline/card/" + Uri.EscapeDataString(activityId));
        Assert.Equal(HttpStatusCode.OK, cardResp.StatusCode);
        var cardHtml = await cardResp.Content.ReadAsStringAsync();

        // The fragment must surface the note text, the like (Alice's single
        // like) and the boost (Bob's single boost) counts.
        Assert.True(cardHtml.Contains(marker), "card fragment missing note text");
        Assert.True(cardHtml.Contains("note-card"), "card fragment missing note-card element");

        // Phase 49.3: the count follows the inline-SVG icon span, not a glyph.
        // The tag helper emits <span aria-hidden="true" class="fb-icon">…</span>,
        // so match the span with class="fb-icon" in any attribute order.
        var likeCountMatch = Regex.Match(cardHtml, @"btn-like\b[^>]*>\s*<span[^>]*class=""fb-icon""[\s\S]*?</span>\s*(\d+)");
        Assert.True(likeCountMatch.Success, "like count not found on card fragment");
        Assert.True(int.Parse(likeCountMatch.Groups[1].Value) >= 1,
            $"Expected at least 1 like, got {likeCountMatch.Groups[1].Value}");

        var boostCountMatch = Regex.Match(cardHtml, @"btn-boost\b[^>]*>\s*<span[^>]*class=""fb-icon""[\s\S]*?</span>\s*(\d+)");
        Assert.True(boostCountMatch.Success, "boost count not found on card fragment");
        Assert.True(int.Parse(boostCountMatch.Groups[1].Value) >= 1,
            $"Expected at least 1 boost, got {boostCountMatch.Groups[1].Value}");
    }

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
    {
        return new FormUrlEncodedContent(fields);
    }
}
