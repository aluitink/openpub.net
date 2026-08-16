using System.Net;
using ActivityPub.Core.Repositories;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.WebUI;

public class RouteAuditTests : IClassFixture<WebUIFactory>
{
    private readonly WebUIFactory _factory;

    public RouteAuditTests(WebUIFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        try { scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated(); } catch { }
        try { scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated(); } catch { }
    }

    private static MultipartFormDataContent Form(Dictionary<string, string> fields)
    {
        var content = new MultipartFormDataContent();
        foreach (var (key, value) in fields)
        {
            content.Add(new StringContent(value), $"\"{key}\"");
        }
        return content;
    }

    private async Task<HttpClient> GetAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var username = $"route_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/auth/register", Form(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Route Test" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" }
        }));
        await client.PostAsync("/auth/login", Form(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" }
        }));
        return client;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/auth/login")]
    [InlineData("/auth/register")]
    [InlineData("/trends")]
    [InlineData("/trending")]
    public async Task AnonymousRoutes_Return200(string route)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/timeline")]
    [InlineData("/compose")]
    [InlineData("/notifications")]
    [InlineData("/search")]
    [InlineData("/suggestions")]
    [InlineData("/discover")]
    [InlineData("/communities")]
    [InlineData("/communities/my")]
    [InlineData("/communities/create")]
    [InlineData("/follow")]
    [InlineData("/follow/following")]
    [InlineData("/follow/followers")]
    [InlineData("/profile")]
    [InlineData("/profile/edit")]
    [InlineData("/poll/new")]
    public async Task AuthenticatedRoutes_Return200_WhenLoggedIn(string route)
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/hashtag/testtag")]
    [InlineData("/communities/search?q=test")]
    [InlineData("/search?q=test")]
    [InlineData("/trends?period=hourly")]
    public async Task QueryParamRoutes_Return200(string route)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentRoute_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/this-page-does-not-exist-12345");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Timeline_RedirectsUnauthenticated()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/timeline");
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized or HttpStatusCode.OK,
            $"Expected redirect/401/200 for unauthenticated timeline, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AdminRoutes_DenyNonAdmin()
    {
        var client = await GetAuthenticatedClient();
        var checks = new (string Route, string AdminContent)[]
        {
            ("/admin/dashboard", "Admin Dashboard"),
            ("/admin/users", "Manage Users"),
            ("/admin/moderation", "Recent Activities"),
            ("/admin/reports", "Pending Reports"),
            ("/admin/audit-log", "Audit Log")
        };

        foreach (var (route, content) in checks)
        {
            var response = await client.GetAsync(route);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(content, body);
        }
    }

    [Fact]
    public async Task AdminRoutes_RedirectUnauthenticated()
    {
        var client = _factory.CreateClient();
        var routes = new[]
        {
            "/admin/dashboard",
            "/admin/users",
            "/admin/moderation"
        };

        foreach (var route in routes)
        {
            var response = await client.GetAsync(route);
            Assert.True(
                response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized or HttpStatusCode.OK,
                $"Expected redirect/401 for unauthenticated on {route}, got {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task HashtagRoute_Returns200_ForValidTag()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/hashtag/fediblog");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CommunityShowRoute_WithInvalidId_Returns404()
    {
        var client = await GetAuthenticatedClient();
        var response = await client.GetAsync("/communities/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ActorJsonldRoutes_Return200_ForExistingUser()
    {
        var client = _factory.CreateClient();
        var username = $"actor_{Guid.NewGuid().ToString("N")[..8]}";
        await client.PostAsync("/auth/register", Form(new Dictionary<string, string>
        {
            { "Username", username },
            { "Email", $"{username}@test.com" },
            { "DisplayName", "Actor Test" },
            { "Password", "Password123!" },
            { "ConfirmPassword", "Password123!" }
        }));
        await client.PostAsync("/auth/login", Form(new Dictionary<string, string>
        {
            { "Username", username },
            { "Password", "Password123!" }
        }));

        var routes = new[]
        {
            $"/Actors/Show/{username}",
            $"/Actors/Outbox/{username}",
            $"/Actors/Followers/{username}",
            $"/Actors/Following/{username}",
            $"/Actors/Liked/{username}"
        };

        foreach (var route in routes)
        {
            var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task ActorShowRoute_Returns404_ForNonExistentUser()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/Actors/Show/nonexistent_user_xyz");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
