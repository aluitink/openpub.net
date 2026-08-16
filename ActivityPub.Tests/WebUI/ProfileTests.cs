using ActivityPub.Core.Interfaces;
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

public class ProfileTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public ProfileTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    HttpClient CreateClient() => _factory.CreateClient();

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

    async Task<HttpClient> GetAuthenticatedClient(string? displayName = null)
    {
        var client = CreateClient();
        var username = $"pt_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username, displayName ?? "Test User");
        await LoginUser(client, username);
        return client;
    }

    async Task<(HttpClient client, string username)> GetAuthenticatedClientWithUsername(string? displayName = null)
    {
        var client = CreateClient();
        var username = $"pt_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(client, username, displayName ?? "Test User");
        await LoginUser(client, username);
        return (client, username);
    }

    [Fact]
    public async Task ProfilePage_RequiresAuthentication()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/profile");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Login", body);
    }

    [Fact]
    public async Task ProfilePage_Returns200_WhenAuthenticated()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/profile");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePage_ShowsDisplayName()
    {
        var client = await GetAuthenticatedClient("Alice Wonder");
        var response = await client.GetAsync("/profile");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Alice Wonder", body);
    }

    [Fact]
    public async Task ProfilePage_ShowsStats()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/profile");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Followers", body);
        Assert.Contains("Following", body);
        Assert.Contains("Joined", body);
    }

    [Fact]
    public async Task ProfilePage_ShowsEditButtonForOwnProfile()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/profile");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Edit Profile", body);
    }

    [Fact]
    public async Task EditProfilePage_Returns200()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/profile/edit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EditProfile_UpdatesDisplayName()
    {
        var (client, username) = await GetAuthenticatedClientWithUsername();

        // Get the antiforgery token from the edit page
        var editGetResponse = await client.GetAsync("/profile/edit");
        var editGetBody = await editGetResponse.Content.ReadAsStringAsync();
        var token = ExtractAntiForgeryToken(editGetBody);

        var editPostContent = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("DisplayName", "New Name"),
            new("Bio", "A new bio"),
            new("IconUrl", "https://example.com/avatar.png"),
            new("BannerUrl", ""),
        };
        var editResponse = await client.PostAsync("/profile/edit", new FormUrlEncodedContent(editPostContent));

        var profileBody = await editResponse.Content.ReadAsStringAsync();
        Assert.Contains("New Name", profileBody);
    }

    static string ExtractAntiForgeryToken(string html)
    {
        var requestMatch = System.Text.RegularExpressions.Regex.Match(html, @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]*)""");
        return requestMatch.Success ? requestMatch.Groups[1].Value : "";
    }

    async Task<string> GetAntiForgeryToken(HttpClient client)
    {
        var response = await client.GetAsync("/profile/edit");
        var body = await response.Content.ReadAsStringAsync();
        return ExtractAntiForgeryToken(body);
    }

    static FormUrlEncodedContent CreateFormContentWithToken(string token, Dictionary<string, string> fields)
    {
        var list = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        list.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));
        return new FormUrlEncodedContent(list);
    }

    [Fact]
    public async Task EditProfile_UpdatesBio()
    {
        var (client, username) = await GetAuthenticatedClientWithUsername("Bio User");

        var editGetResponse = await client.GetAsync("/profile/edit");
        var editGetBody = await editGetResponse.Content.ReadAsStringAsync();
        var token = ExtractAntiForgeryToken(editGetBody);

        var editPostContent = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("DisplayName", "Bio User"),
            new("Bio", "Hello, I am a bio user."),
            new("IconUrl", ""),
            new("BannerUrl", ""),
        };

        var editResponse = await client.PostAsync("/profile/edit", new FormUrlEncodedContent(editPostContent));
        var responseBody = await editResponse.Content.ReadAsStringAsync();

        // Check for the bio text in the response
        Assert.Contains("Hello, I am a bio user.", responseBody);
    }

    [Fact]
    public async Task EditProfile_WithEmptyDisplayName_ReturnsError()
    {
        var client = await GetAuthenticatedClient();
        var token = await GetAntiForgeryToken(client);

        var editResponse = await client.PostAsync("/profile/edit", CreateFormContentWithToken(token, new Dictionary<string, string>
        {
            { "DisplayName", "" },
            { "Bio", "" },
            { "IconUrl", "" },
            { "BannerUrl", "" },
        }));

        var editBody = await editResponse.Content.ReadAsStringAsync();
        Assert.Contains("DisplayName", editBody);
    }

    [Fact]
    public async Task EditProfile_WithLongBio_ReturnsError()
    {
        var client = await GetAuthenticatedClient();
        var token = await GetAntiForgeryToken(client);

        var editResponse = await client.PostAsync("/profile/edit", CreateFormContentWithToken(token, new Dictionary<string, string>
        {
            { "DisplayName", "Valid Name" },
            { "Bio", new string('x', 501) },
            { "IconUrl", "" },
            { "BannerUrl", "" },
        }));

        var editBody = await editResponse.Content.ReadAsStringAsync();
        Assert.Contains("500", editBody);
    }

    [Fact]
    public async Task ActorEndpoint_ReturnsPersonJson()
    {
        var (client, username) = await GetAuthenticatedClientWithUsername("Actor Test");

        var response = await client.GetAsync($"/actors/show/{username}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Actor endpoint failed: {(int)response.StatusCode} - {body.Substring(0, Math.Min(200, body.Length))}");

        var content = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/ld+json", content);
        Assert.Contains("Person", body);
        Assert.Contains("Actor Test", body);
        Assert.Contains("activitystreams", body);
    }

    [Fact]
    public async Task ActorEndpoint_Returns404ForUnknownUser()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/actors/show/nonexistent_user_12345");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OutboxEndpoint_ReturnsOrderedCollection()
    {
        var (client, username) = await GetAuthenticatedClientWithUsername();

        await client.PostAsync("/compose/post", CreateFormContent(new Dictionary<string, string>
        {
            { "Content", "Outbox test post" },
        }));

        var response = await client.GetAsync($"/actors/outbox/{username}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("OrderedCollection", body);
        Assert.Contains("totalItems", body);
    }

    [Fact]
    public async Task FollowersEndpoint_ReturnsOrderedCollection()
    {
        var (client, username) = await GetAuthenticatedClientWithUsername();

        var response = await client.GetAsync($"/actors/followers/{username}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("OrderedCollection", body);
        Assert.Contains("totalItems", body);
    }

    [Fact]
    public async Task FollowingEndpoint_ReturnsOrderedCollection()
    {
        var (client, username) = await GetAuthenticatedClientWithUsername();

        var response = await client.GetAsync($"/actors/following/{username}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("OrderedCollection", body);
        Assert.Contains("totalItems", body);
    }

    [Fact]
    public async Task LikedEndpoint_ReturnsOrderedCollection()
    {
        var (client, username) = await GetAuthenticatedClientWithUsername();

        var response = await client.GetAsync($"/actors/liked/{username}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("OrderedCollection", body);
        Assert.Contains("totalItems", body);
    }

    [Fact]
    public async Task ViewOtherUserProfile()
    {
        var authorClient = CreateClient();
        var authorName = $"author_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(authorClient, authorName, "Author Display");
        await LoginUser(authorClient, authorName);

        var viewerClient = CreateClient();
        var viewerName = $"viewer_{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterUser(viewerClient, viewerName, "Viewer Display");
        await LoginUser(viewerClient, viewerName);

        var response = await viewerClient.GetAsync($"/profile?username={authorName}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Author Display", body);
        Assert.Contains(authorName, body);
    }

    static FormUrlEncodedContent CreateFormContent(Dictionary<string, string> fields)
    {
        return new FormUrlEncodedContent(fields);
    }
}
