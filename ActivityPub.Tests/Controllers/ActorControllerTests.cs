using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core;
using ActivityPub.Core.Tests;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Core.Tests.Controllers;

public class ActorControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActorControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task InitializeTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var actor = new Actor
        {
            Id = "https://localhost/users/testuser",
            Type = "Person",
            Name = "Test User",
            PreferredUsername = "testuser",
            Inbox = "https://localhost/users/testuser/inbox",
            Outbox = "https://localhost/users/testuser/outbox"
        };

        await repository.SaveUserActorAsync(actor);
    }

    [Fact]
    public async Task ActorController_GetActor_ReturnsActor_WhenUserExists()
    {
        await InitializeTestData();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/users/testuser");

        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Response was {response.StatusCode}: {content}");
        Assert.Contains("\"type\":\"Person\"", content);
        Assert.Contains("\"preferredUsername\":\"testuser\"", content);
    }

    [Fact]
    public async Task ActorController_GetActor_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/users/nonexistentuser");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ActorController_GetOutbox_ReturnsOrderedCollection()
    {
        await InitializeTestData();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/users/testuser/outbox");

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"type\":\"OrderedCollection\"", content);
    }

    [Fact]
    public async Task ActorController_GetFollowers_ReturnsCollection()
    {
        await InitializeTestData();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/users/testuser/followers");

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"type\":\"Collection\"", content);
    }

    [Fact]
    public async Task ActorController_GetFollowing_ReturnsCollection()
    {
        await InitializeTestData();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/users/testuser/following");

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"type\":\"Collection\"", content);
    }

    [Fact]
    public async Task ActorController_GetInbox_ReturnsOrderedCollection()
    {
        await InitializeTestData();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/users/testuser/inbox");

        Assert.True(response.IsSuccessStatusCode, $"Response was {response.StatusCode}");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"type\":\"OrderedCollection\"", content);
    }

    [Fact]
    public async Task ActorController_GetLiked_ReturnsCollection()
    {
        await InitializeTestData();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/users/testuser/liked");

        Assert.True(response.IsSuccessStatusCode, $"Response was {response.StatusCode}");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"type\":\"Collection\"", content);
    }

    [Fact]
    public async Task ActorController_PostInbox_ReturnsSuccess()
    {
        await InitializeTestData();

        var client = _factory.CreateClient();
        var activity = new
        {
            id = $"https://localhost/activities/{Guid.NewGuid()}",
            type = "Create",
            actor = "https://localhost/users/testuser"
        };
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(activity),
            System.Text.Encoding.UTF8,
            "application/activity+json");

        var response = await client.PostAsync("/users/testuser/inbox", content);

        Assert.True(response.IsSuccessStatusCode, $"Response was {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task ActorController_PostOutbox_ReturnsSuccess()
    {
        await InitializeTestData();

        var client = _factory.CreateClient();
        var activity = new
        {
            Id = $"https://localhost/activities/{Guid.NewGuid()}",
            Type = "Create",
            Actor = "https://localhost/users/testuser"
        };
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(activity),
            System.Text.Encoding.UTF8,
            "application/activity+json");

        var response = await client.PostAsync("/users/testuser/outbox", content);

        Assert.True(response.IsSuccessStatusCode, $"Response was {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task ActorController_GetActor_ReturnsPublicKey()
    {
        await InitializeTestData();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/users/testuser");

        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Response was {response.StatusCode}: {content}");
        Assert.Contains("\"publicKey\"", content);
    }
}
