using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core;
using ActivityPub.Core.API.Controllers.Federation;
using ActivityPub.Core.Tests;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

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

    // ------------------------------------------------------------------
    // PostInbox error-handling unit tests (Phase 38 Task 5)
    // ------------------------------------------------------------------

    private static ActorController CreateInboxController(
        Mock<IActivityPubRepository> repository,
        Mock<ISharedInboxService> sharedInboxService,
        string? rawBody = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/users/testuser/inbox";
        if (rawBody is not null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(rawBody);
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.Body.Position = 0;
            httpContext.Request.ContentLength = bytes.Length;
            httpContext.Request.ContentType = "application/activity+json";
        }

        // The controller resolves ISharedInboxService from RequestServices,
        // so wire up a minimal service provider that returns the mock.
        var services = new ServiceCollection();
        services.AddSingleton(sharedInboxService.Object);
        var provider = services.BuildServiceProvider();
        httpContext.RequestServices = provider;

        var options = new Microsoft.Extensions.Options.OptionsWrapper<ActivityPubOptions>(new ActivityPubOptions());
        var controller = new ActorController(repository.Object, Mock.Of<ILogger<ActorController>>(), options);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    [Fact]
    public async Task PostInbox_NullActivity_Returns400()
    {
        var repo = new Mock<IActivityPubRepository>();
        var sharedInbox = new Mock<ISharedInboxService>();
        var controller = CreateInboxController(repo, sharedInbox);

        var result = await controller.PostInbox("testuser", null!);

        // The controller uses the BadRequest(string) overload, which produces
        // a BadRequestObjectResult in ASP.NET Core 8+.
        var badRequest = result as Microsoft.AspNetCore.Mvc.BadRequestObjectResult
            ?? (Microsoft.AspNetCore.Mvc.BadRequestObjectResult)(object)result;
        Assert.NotNull(badRequest);
    }

    [Fact]
    public async Task PostInbox_ServiceThrows_Returns500()
    {
        var repo = new Mock<IActivityPubRepository>();
        var sharedInbox = new Mock<ISharedInboxService>();
        sharedInbox
            .Setup(s => s.ProcessAndDistributeActivityAsync(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("simulated db failure"));

        var rawBody = "{\"id\":\"act-1\",\"type\":\"Create\",\"actor\":\"https://remote.example/users/alice\"}";
        var controller = CreateInboxController(repo, sharedInbox, rawBody: rawBody);
        var activity = new Activity
        {
            Id = "act-1",
            Type = "Create",
            Actor = "https://remote.example/users/alice"
        };

        var result = await controller.PostInbox("testuser", activity);

        // StatusCode(500, string) returns a StatusCodeResult; but the
        // controller's catch block uses StatusCode(500, "...") which may
        // produce a different result type. Accept any result with status 500.
        var status = result switch
        {
            Microsoft.AspNetCore.Mvc.StatusCodeResult s => s.StatusCode,
            Microsoft.AspNetCore.Mvc.ObjectResult o => o.StatusCode,
            _ => null
        };
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task PostInbox_ServiceReturnsTrue_Returns200()
    {
        var repo = new Mock<IActivityPubRepository>();
        var sharedInbox = new Mock<ISharedInboxService>();
        sharedInbox
            .Setup(s => s.ProcessAndDistributeActivityAsync(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<string?>()))
            .ReturnsAsync(true);

        var rawBody = "{\"id\":\"act-2\",\"type\":\"Create\",\"actor\":\"https://remote.example/users/alice\"}";
        var controller = CreateInboxController(repo, sharedInbox, rawBody: rawBody);
        var activity = new Activity
        {
            Id = "act-2",
            Type = "Create",
            Actor = "https://remote.example/users/alice"
        };

        var result = await controller.PostInbox("testuser", activity);

        // Content() with a string body and content-type returns a ContentResult
        // with StatusCode defaulting to null (meaning 200 OK).
        var contentResult = result as Microsoft.AspNetCore.Mvc.ContentResult;
        Assert.NotNull(contentResult);
        Assert.True(contentResult.StatusCode == null || contentResult.StatusCode == 200);
        Assert.Contains("true", contentResult.Content);
    }

    [Fact]
    public async Task PostInbox_ServiceReturnsFalse_Returns400()
    {
        var repo = new Mock<IActivityPubRepository>();
        var sharedInbox = new Mock<ISharedInboxService>();
        sharedInbox
            .Setup(s => s.ProcessAndDistributeActivityAsync(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<string?>()))
            .ReturnsAsync(false);

        var rawBody = "{\"id\":\"act-3\",\"type\":\"Create\",\"actor\":\"https://remote.example/users/alice\"}";
        var controller = CreateInboxController(repo, sharedInbox, rawBody: rawBody);
        var activity = new Activity
        {
            Id = "act-3",
            Type = "Create",
            Actor = "https://remote.example/users/alice"
        };

        var result = await controller.PostInbox("testuser", activity);

        var badRequest = result as Microsoft.AspNetCore.Mvc.BadRequestObjectResult
            ?? (Microsoft.AspNetCore.Mvc.BadRequestObjectResult)(object)result;
        Assert.NotNull(badRequest);
    }

    [Fact]
    public async Task PostInbox_CapturesRawJsonFromBody()
    {
        var repo = new Mock<IActivityPubRepository>();
        var sharedInbox = new Mock<ISharedInboxService>();
        string? capturedRawJson = null;
        sharedInbox
            .Setup(s => s.ProcessAndDistributeActivityAsync(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<string?>()))
            .Callback<string, Activity, string?>((_, _, rawJson) => capturedRawJson = rawJson)
            .ReturnsAsync(true);

        var rawBody = "{\"id\":\"act-4\",\"type\":\"Create\",\"actor\":\"https://remote.example/users/alice\"}";
        var controller = CreateInboxController(repo, sharedInbox, rawBody: rawBody);
        var activity = new Activity
        {
            Id = "act-4",
            Type = "Create",
            Actor = "https://remote.example/users/alice"
        };

        await controller.PostInbox("testuser", activity);

        Assert.NotNull(capturedRawJson);
        Assert.Equal(rawBody, capturedRawJson);
    }
}
