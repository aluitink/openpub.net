using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class FollowTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public FollowTests(WebUIFactory factory)
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
        var username = $"ft_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username);
        await LoginUser(client, username);
        return (client, username);
    }

    [Fact]
    public async Task FollowPage_RequiresAuthentication()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/follow");
        Assert.Contains("Login", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FollowingPage_RequiresAuthentication()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/follow/following");
        Assert.Contains("Login", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FollowersPage_RequiresAuthentication()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/follow/followers");
        Assert.Contains("Login", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FollowPage_Returns200_WhenAuthenticated()
    {
        var (client, _) = await GetAuthClientWithUser();
        var response = await client.GetAsync("/follow");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FollowingPage_Returns200_WhenAuthenticated()
    {
        var (client, _) = await GetAuthClientWithUser();
        var response = await client.GetAsync("/follow/following");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FollowersPage_Returns200_WhenAuthenticated()
    {
        var (client, _) = await GetAuthClientWithUser();
        var response = await client.GetAsync("/follow/followers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FollowLocalUser_Succeeds()
    {
        var (client, followerName) = await GetAuthClientWithUser();

        var targetName = $"ft_target_{Guid.NewGuid().ToString("N")[..8]}";
        var registerResp = await client.PostAsync("/auth/register", CreateFormContent(new Dictionary<string, string>
        {
            { "Username", targetName },
            { "Email", $"{targetName}@test.com" },
            { "DisplayName", targetName },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" },
        }));
        Assert.True(registerResp.IsSuccessStatusCode || registerResp.Headers.Location != null);

        var followResp = await client.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
        {
            { "Handle", targetName },
        }));
        Assert.True(followResp.IsSuccessStatusCode || followResp.Headers.Location != null,
            $"Follow failed: {(int)followResp.StatusCode}");

        var followingResp = await client.GetAsync("/follow/following");
        var followingBody = await followingResp.Content.ReadAsStringAsync();
        Assert.Contains(targetName, followingBody);
    }

    [Fact]
    public async Task FollowSelf_ReturnsError()
    {
        var (client, username) = await GetAuthClientWithUser();

        var followResp = await client.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
        {
            { "Handle", username },
        }));

        var body = await followResp.Content.ReadAsStringAsync();
        Assert.Contains("cannot follow yourself", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FollowSameUserTwice_ReturnsError()
    {
        var client = CreateClient();
        var followerName = $"ft_dup_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, followerName);
        await LoginUser(client, followerName);

        var targetName = $"ft_target2_{Guid.NewGuid().ToString("N")[..8]}";
        var targetClient = CreateClient();
        await RegisterUser(targetClient, targetName);
        await LoginUser(targetClient, targetName);

        await client.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
        {
            { "Handle", targetName },
        }));

        var followingResp = await client.GetAsync("/follow/following");
        var followingBody = await followingResp.Content.ReadAsStringAsync();
        Assert.True(followingBody.Contains(targetName), $"Target not in following list. Following page: {followingBody}");

        await LoginUser(client, followerName);

        var dupResp = await client.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
        {
            { "Handle", targetName },
        }));

        var body = await dupResp.Content.ReadAsStringAsync();

        if ((int)dupResp.StatusCode == 302 && dupResp.Headers.Location != null)
        {
            var redirectResp = await client.GetAsync(dupResp.Headers.Location.PathAndQuery);
            body = await redirectResp.Content.ReadAsStringAsync();
        }

        Assert.True(body.Contains("already following", StringComparison.OrdinalIgnoreCase),
            $"Expected 'already following' error. Status: {(int)dupResp.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Unfollow_RemovesFromFollowing()
    {
        var (client, followerName) = await GetAuthClientWithUser();

        var targetName = $"ft_target3_{Guid.NewGuid().ToString("N")[..8]}";
        var targetClient = CreateClient();
        await RegisterUser(targetClient, targetName);
        await LoginUser(targetClient, targetName);

        await client.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
        {
            { "Handle", targetName },
        }));

        var followingResp = await client.GetAsync("/follow/following");
        var followingBody = await followingResp.Content.ReadAsStringAsync();
        Assert.Contains(targetName, followingBody);

        var actorIdMatch = Regex.Match(followingBody, @"name=""actorId""[^>]*value=""([^""]+)""");
        Assert.True(actorIdMatch.Success, "actorId not found in following page");
        var actorId = actorIdMatch.Groups[1].Value;

        var tokenMatch = Regex.Match(followingBody, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        var antiforgeryToken = tokenMatch.Success ? tokenMatch.Groups[1].Value : null;

        var content = antiforgeryToken != null
            ? CreateFormContent(new Dictionary<string, string>
            {
                { "actorId", actorId },
                { "__RequestVerificationToken", antiforgeryToken },
            })
            : CreateFormContent(new Dictionary<string, string>
            {
                { "actorId", actorId },
            });

        var unfollowResp = await client.PostAsync("/follow/unfollow", content);
        var unfollowBody = await unfollowResp.Content.ReadAsStringAsync();

        Assert.DoesNotContain(targetName, unfollowBody);
    }

    [Fact]
    public async Task Follow_WithAtDomain_Format()
    {
        var (client, followerName) = await GetAuthClientWithUser();

        var targetName = $"ft_target4_{Guid.NewGuid().ToString("N")[..8]}";
        var targetClient = CreateClient();
        await RegisterUser(targetClient, targetName);
        await LoginUser(targetClient, targetName);

        var followResp = await client.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
        {
            { "Handle", $"{targetName}@localhost" },
        }));

        Assert.True(followResp.IsSuccessStatusCode || followResp.Headers.Location != null,
            $"Follow failed: {(int)followResp.StatusCode}");

        var followingResp = await client.GetAsync("/follow/following");
        var followingBody = await followingResp.Content.ReadAsStringAsync();
        Assert.Contains(targetName, followingBody);
    }

    [Fact]
    public async Task FollowWithEmptyHandle_ReturnsError()
    {
        var (client, _) = await GetAuthClientWithUser();

        var followResp = await client.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
        {
            { "Handle", "" },
        }));

        var body = await followResp.Content.ReadAsStringAsync();
        Assert.Contains("Enter a handle", body);
    }

    [Fact]
    public async Task FollowNonExistentUser_ReturnsError()
    {
        var (client, _) = await GetAuthClientWithUser();

        var followResp = await client.PostAsync("/follow/follow", CreateFormContent(new Dictionary<string, string>
        {
            { "Handle", $"nonexistent_{Guid.NewGuid().ToString("N")[..8]}" },
        }));

        var body = await followResp.Content.ReadAsStringAsync();
        Assert.Contains("Could not find actor", body);
    }

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
    {
        return new FormUrlEncodedContent(fields);
    }
}
