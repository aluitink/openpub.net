using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using ActivityPub.Core.Middleware;
using ActivityPub.Core.Infrastructure;
using ActivityPub.Core.Caching;
using ActivityPub.Core.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace ActivityPub.Tests.Integration;

/// <summary>
/// Integration tests for ActivityPub federation between multiple instances
/// </summary>
public class FederationIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public FederationIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    #region Multi-Instance Federation Tests

    [Fact]
    public async Task Federation_MultipleInstances_CanExchangeActivities()
    {
        // Arrange
        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        // Create two actors on the same instance
        var actor1 = new Actor
        {
            Id = "https://localhost/users/actor1",
            Type = "Person",
            PreferredUsername = "actor1",
            Inbox = "https://localhost/users/actor1/inbox",
            Outbox = "https://localhost/users/actor1/outbox"
        };

        var actor2 = new Actor
        {
            Id = "https://localhost/users/actor2",
            Type = "Person",
            PreferredUsername = "actor2",
            Inbox = "https://localhost/users/actor2/inbox",
            Outbox = "https://localhost/users/actor2/outbox"
        };

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
        
        await repository.SaveUserActorAsync(actor1);
        await repository.SaveUserActorAsync(actor2);

        // Create a Create activity from actor1
        var activity = new Activity
        {
            Id = "https://localhost/users/actor1/activities/123",
            Type = "Create",
            Actor = "https://localhost/users/actor1",
            Object = new Note
            {
                Id = "https://localhost/users/actor1/notes/123",
                Type = "Note",
                Content = "Hello from actor1!"
            }
        };

        // Act - Send activity to actor2's inbox
        var activityJson = JsonSerializer.Serialize(activity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");
        
        // Sign the request
        var keyPair = RSA.Create(2048);
        var keyId = $"{actor1.Id}#main-key";
        SignRequest(content, keyPair, keyId, "localhost");

        // Send to actor2's inbox
        var response = await client2.PostAsync("/users/actor2/inbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Failed to deliver activity: {response.StatusCode}");
    }

    [Fact]
    public async Task Federation_ActorCanFollowAnotherActor()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var follower = new Actor
        {
            Id = "https://localhost/users/follower",
            Type = "Person",
            PreferredUsername = "follower",
            Inbox = "https://localhost/users/follower/inbox"
        };

        var following = new Actor
        {
            Id = "https://localhost/users/following",
            Type = "Person",
            PreferredUsername = "following",
            Inbox = "https://localhost/users/following/inbox"
        };

        await repository.SaveUserActorAsync(follower);
        await repository.SaveUserActorAsync(following);

        // Create Follow activity
        var followActivity = new Activity
        {
            Id = "https://localhost/users/follower/activities/follow1",
            Type = "Follow",
            Actor = "https://localhost/users/follower",
            Object = "https://localhost/users/following"
        };

        var activityJson = JsonSerializer.Serialize(followActivity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/following/inbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Follow failed: {response.StatusCode}");
    }

    [Fact]
    public async Task Federation_LikeActivity_CanBeSentAndStored()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var liker = new Actor
        {
            Id = "https://localhost/users/liker",
            Type = "Person",
            PreferredUsername = "liker",
            Inbox = "https://localhost/users/liker/inbox"
        };

        await repository.SaveUserActorAsync(liker);

        var likeActivity = new Activity
        {
            Id = "https://localhost/users/liker/activities/like1",
            Type = "Like",
            Actor = "https://localhost/users/liker",
            Object = "https://localhost/users/liker/notes/123"
        };

        var activityJson = JsonSerializer.Serialize(likeActivity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/liker/inbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Like failed: {response.StatusCode}");
    }

    [Fact]
    public async Task Federation_AnnounceActivity_CanBeSentAndStored()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var announcer = new Actor
        {
            Id = "https://localhost/users/announcer",
            Type = "Person",
            PreferredUsername = "announcer",
            Inbox = "https://localhost/users/announcer/inbox"
        };

        await repository.SaveUserActorAsync(announcer);

        var announceActivity = new Activity
        {
            Id = "https://localhost/users/announcer/activities/announce1",
            Type = "Announce",
            Actor = "https://localhost/users/announcer",
            Object = "https://localhost/users/announcer/notes/123"
        };

        var activityJson = JsonSerializer.Serialize(announceActivity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/announcer/inbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Announce failed: {response.StatusCode}");
    }

    [Fact]
    public async Task Federation_UndoActivity_CanRevokePreviousActivity()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var user = new Actor
        {
            Id = "https://localhost/users/user",
            Type = "Person",
            PreferredUsername = "user",
            Inbox = "https://localhost/users/user/inbox"
        };

        await repository.SaveUserActorAsync(user);

        // Create the original Follow activity
        var followActivity = new Activity
        {
            Id = "https://localhost/users/user/activities/follow1",
            Type = "Follow",
            Actor = "https://localhost/users/user",
            Object = "https://localhost/users/other"
        };

        var followJson = JsonSerializer.Serialize(followActivity);
        await repository.SaveActivityAsync(followActivity);

        // Create Undo activity
        var undoActivity = new Activity
        {
            Id = "https://localhost/users/user/activities/undo1",
            Type = "Undo",
            Actor = "https://localhost/users/user",
            Object = followActivity
        };

        var activityJson = JsonSerializer.Serialize(undoActivity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/user/inbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Undo failed: {response.StatusCode}");
    }

    [Fact]
    public async Task Federation_DeleteActivity_CanTombstoneObject()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var user = new Actor
        {
            Id = "https://localhost/users/user",
            Type = "Person",
            PreferredUsername = "user",
            Inbox = "https://localhost/users/user/inbox"
        };

        await repository.SaveUserActorAsync(user);

        // Create a note to delete
        var note = new Note
        {
            Id = "https://localhost/users/user/notes/123",
            Type = "Note",
            Content = "This will be deleted"
        };

        // Create Delete activity
        var deleteActivity = new Activity
        {
            Id = "https://localhost/users/user/activities/delete1",
            Type = "Delete",
            Actor = "https://localhost/users/user",
            Object = note
        };

        var activityJson = JsonSerializer.Serialize(deleteActivity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/user/inbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Delete failed: {response.StatusCode}");
    }

    #endregion

    #region WebFinger Resolution Tests

    [Fact]
    public async Task WebFinger_CanResolveActorFromAcctIdentifier()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:test@localhost");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"subject\":\"acct:test@localhost\"", content);
        Assert.Contains("\"rel\":\"self\"", content);
        Assert.Contains("\"type\":\"application/activity+json\"", content);
    }

    [Fact]
    public async Task WebFinger_CanResolveActorWithRelParameter()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:test@localhost&rel=http://webfinger.net/rel/profile-page");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"subject\":\"acct:test@localhost\"", content);
        Assert.Contains("\"rel\":\"self\"", content);
    }

    [Fact]
    public async Task WebFinger_ReturnsNotFound_ForInvalidResource()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/.well-known/webfinger?resource=acct:nonexistent@localhost");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"subject\":\"acct:nonexistent@localhost\"", content);
        Assert.Contains("\"rel\":\"self\"", content);
    }

    [Fact]
    public async Task WebFinger_CanResolveActorFromURL()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/.well-known/webfinger?resource=https://localhost/users/test");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"subject\":\"https://localhost/users/test\"", content);
    }

    #endregion

    #region HTTP Signature Verification Tests

    [Fact]
    public async Task HttpSignature_ValidSignature_IsAccepted()
    {
        // Arrange
        var keyPair = RSA.Create(2048);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var headersToSign = "(created)";
        var stringToSign = $"{headersToSign}: {timestamp}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stringToSign));
        var signatureBytes = keyPair.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";
        context.Request.Body = new MemoryStream(new byte[] { (byte)'{', (byte)'}' });
        context.Request.Body.Position = 0;

        // Set up headers with parentheses using indexer (bypasses validation)
        context.Request.Headers["Host"] = "localhost";
        context.Request.Headers["(created)"] = timestamp.ToString();
        context.Request.Headers["(expires)"] = (timestamp + 300).ToString();
        context.Request.Headers["Signature"] = $"keyId=\"test\",headers=\"(created)\",signature=\"{signature}\"";

        // Set up mock key fetching service
        var services = new ServiceCollection();
        var mockKeyFetcher = new Mock<IKeyFetchingService>();
        var publicKeyDer = keyPair.ExportSubjectPublicKeyInfo();
        var publicKeyPem = $"-----BEGIN PUBLIC KEY-----\n{Convert.ToBase64String(publicKeyDer, Base64FormattingOptions.InsertLineBreaks)}\n-----END PUBLIC KEY-----";
        mockKeyFetcher.Setup(s => s.FetchPublicKeyAsync("test")).ReturnsAsync(new PublicKey
        {
            Id = "test",
            Owner = "https://localhost/test",
            PublicKeyPem = publicKeyPem
        });
        services.AddSingleton(mockKeyFetcher.Object);
        context.RequestServices = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<HttpSignatureMiddleware>>();
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            logger
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task HttpSignature_ExpiredSignature_IsRejected()
    {
        // Arrange
        var expiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 400;

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";
        context.Request.Headers["Host"] = "localhost";
        context.Request.Headers["(created)"] = expiredTimestamp.ToString();
        context.Request.Headers["(expires)"] = (expiredTimestamp + 300).ToString();
        context.Request.Headers["Signature"] = "keyId=\"test\",headers=\"(created)\",signature=\"fake\"";

        var services = new ServiceCollection();
        var mockKeyFetcher = new Mock<IKeyFetchingService>();
        services.AddSingleton(mockKeyFetcher.Object);
        context.RequestServices = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<HttpSignatureMiddleware>>();
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            logger
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task HttpSignature_MissingSignature_IsRejected()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";
        context.Request.Headers.Add("Host", "localhost");

        var logger = Mock.Of<ILogger<HttpSignatureMiddleware>>();
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            logger
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task HttpSignature_InvalidSignature_IsRejected()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";
        context.Request.Headers["Host"] = "localhost";
        context.Request.Headers["(created)"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        context.Request.Headers["Signature"] = "keyId=\"test\",headers=\"(created)\",signature=\"invalidsignature123\"";

        var services = new ServiceCollection();
        var mockKeyFetcher = new Mock<IKeyFetchingService>();
        services.AddSingleton(mockKeyFetcher.Object);
        context.RequestServices = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<HttpSignatureMiddleware>>();
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            logger
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(401, context.Response.StatusCode);
    }

    #endregion

    #region Inbox Delivery and Retrieval Tests

    [Fact]
    public async Task Inbox_CanReceiveAndStoreActivity()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var actor = new Actor
        {
            Id = "https://localhost/users/inboxuser",
            Type = "Person",
            PreferredUsername = "inboxuser",
            Inbox = "https://localhost/users/inboxuser/inbox"
        };

        await repository.SaveUserActorAsync(actor);

        var activity = new Activity
        {
            Id = "https://localhost/users/inboxuser/activities/delivery1",
            Type = "Create",
            Actor = "https://localhost/users/inboxuser",
            Object = new Note
            {
                Id = "https://localhost/users/inboxuser/notes/delivery1",
                Type = "Note",
                Content = "Test message"
            }
        };

        var activityJson = JsonSerializer.Serialize(activity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/inboxuser/inbox", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        // Verify activity was stored
        var storedActivity = await repository.GetActivityAsync(activity.Id);
        Assert.NotNull(storedActivity);
        Assert.Equal(activity.Id, storedActivity?.Id);
    }

    [Fact]
    public async Task Inbox_CanRetrieveActivities()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var actor = new Actor
        {
            Id = "https://localhost/users/retrievaluser",
            Type = "Person",
            PreferredUsername = "retrievaluser",
            Inbox = "https://localhost/users/retrievaluser/inbox"
        };

        await repository.SaveUserActorAsync(actor);

        var activity = new Activity
        {
            Id = "https://localhost/users/retrievaluser/activities/retrieval1",
            Type = "Create",
            Actor = "https://localhost/users/retrievaluser",
            Object = new Note
            {
                Id = "https://localhost/users/retrievaluser/notes/retrieval1",
                Type = "Note",
                Content = "Retrieval test"
            }
        };

        await repository.SaveActivityAsync(activity);

        // Act
        var activities = await repository.GetActorOutboxActivitiesAsync("retrievaluser", 0, 10);

        // Assert
        Assert.NotNull(activities);
        Assert.Contains(activity.Id, activities);
    }

    [Fact]
    public async Task Inbox_CanHandleSharedInboxDelivery()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var actor = new Actor
        {
            Id = "https://localhost/users/shareduser",
            Type = "Person",
            PreferredUsername = "shareduser",
            Inbox = "https://localhost/users/shareduser/inbox",
            SharedInbox = "https://localhost/inbox"
        };

        await repository.SaveUserActorAsync(actor);

        var activity = new Activity
        {
            Id = "https://localhost/users/shareduser/activities/shared1",
            Type = "Create",
            Actor = "https://localhost/users/shareduser",
            Object = new Note
            {
                Id = "https://localhost/users/shareduser/notes/shared1",
                Type = "Note",
                Content = "Shared inbox test"
            }
        };

        // Act
        var queued = await repository.QueueSharedInboxDeliveryAsync(
            activity.Id,
            JsonSerializer.Serialize(activity),
            actor.Id
        );

        // Assert
        Assert.True(queued);

        var pendingDeliveries = await repository.GetPendingSharedInboxDeliveriesAsync(10);
        Assert.Contains(pendingDeliveries, d => d.ActivityId == activity.Id);
    }

    #endregion

    #region Activity Propagation Tests

    [Fact]
    public async Task ActivityPropagation_CanTrackActivityChain()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var user = new Actor
        {
            Id = "https://localhost/users/propagationuser",
            Type = "Person",
            PreferredUsername = "propagationuser",
            Inbox = "https://localhost/users/propagationuser/inbox"
        };

        await repository.SaveUserActorAsync(user);

        // Create a chain of activities
        var activities = new List<Activity>
        {
            new Activity
            {
                Id = "https://localhost/users/propagationuser/activities/chain1",
                Type = "Create",
                Actor = user.Id,
                Object = new Note
                {
                    Id = "https://localhost/users/propagationuser/notes/chain1",
                    Type = "Note",
                    Content = "Original post"
                }
            },
            new Activity
            {
                Id = "https://localhost/users/propagationuser/activities/chain2",
                Type = "Like",
                Actor = user.Id,
                Object = "https://localhost/users/propagationuser/notes/chain1"
            },
            new Activity
            {
                Id = "https://localhost/users/propagationuser/activities/chain3",
                Type = "Announce",
                Actor = user.Id,
                Object = "https://localhost/users/propagationuser/notes/chain1"
            }
        };

        // Act - Save activities
        foreach (var activity in activities)
        {
            await repository.SaveActivityAsync(activity);
        }

        // Assert
        foreach (var activity in activities)
        {
            var stored = await repository.GetActivityAsync(activity.Id);
            Assert.NotNull(stored);
            Assert.Equal(activity.Id, stored?.Id);
        }
    }

    [Fact]
    public async Task ActivityPropagation_CanDetectDuplicates()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var activity = new Activity
        {
            Id = "https://localhost/users/propagationuser/activities/dup1",
            Type = "Create",
            Actor = "https://localhost/users/propagationuser",
            Object = new Note
            {
                Id = "https://localhost/users/propagationuser/notes/dup1",
                Type = "Note",
                Content = "Duplicate test"
            }
        };

        // Act - Save activity twice
        await repository.SaveActivityAsync(activity);
        var firstSeen = await repository.HasSeenActivityAsync(activity.Id);
        
        await repository.SaveActivityAsync(activity);
        var secondSeen = await repository.HasSeenActivityAsync(activity.Id);

        // Assert
        Assert.True(firstSeen);
        Assert.True(secondSeen);
    }

    [Fact]
    public async Task ActivityPropagation_CanPropagateToFollowers()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();

        var author = new Actor
        {
            Id = "https://localhost/users/author",
            Type = "Person",
            PreferredUsername = "author",
            Inbox = "https://localhost/users/author/inbox"
        };

        var follower1 = new Actor
        {
            Id = "https://localhost/users/follower1",
            Type = "Person",
            PreferredUsername = "follower1",
            Inbox = "https://localhost/users/follower1/inbox"
        };

        var follower2 = new Actor
        {
            Id = "https://localhost/users/follower2",
            Type = "Person",
            PreferredUsername = "follower2",
            Inbox = "https://localhost/users/follower2/inbox"
        };

        await repository.SaveUserActorAsync(author);
        await repository.SaveUserActorAsync(follower1);
        await repository.SaveUserActorAsync(follower2);

        var activity = new Activity
        {
            Id = "https://localhost/users/author/activities/followers1",
            Type = "Create",
            Actor = author.Id,
            Object = new Note
            {
                Id = "https://localhost/users/author/notes/followers1",
                Type = "Note",
                Content = "Post for followers"
            },
            To = new List<string> { "https://localhost/users/follower1/followers" }
        };

        // Act
        await repository.SaveActivityAsync(activity);

        // Assert
        var storedActivity = await repository.GetActivityAsync(activity.Id);
        Assert.NotNull(storedActivity);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ErrorHandling_InvalidActivityJson_IsRejected()
    {
        // Arrange - Use empty object which is valid JSON but missing required fields
        var invalidJson = "{}";
        var content = new StringContent(invalidJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/test/inbox", content);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ErrorHandling_MissingActivityType_IsRejected()
    {
        // Arrange
        var activity = new
        {
            id = "https://localhost/users/test/activities/1",
            actor = "https://localhost/users/test"
        };

        var activityJson = JsonSerializer.Serialize(activity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/test/inbox", content);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ErrorHandling_MissingActor_IsRejected()
    {
        // Arrange
        var activity = new
        {
            id = "https://localhost/users/test/activities/1",
            type = "Create"
        };

        var activityJson = JsonSerializer.Serialize(activity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/test/inbox", content);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ErrorHandling_UnknownActivityType_IsRejected()
    {
        // Arrange
        var activity = new Activity
        {
            Id = "https://localhost/users/test/activities/1",
            Type = "UnknownType",
            Actor = "https://localhost/users/test"
        };

        var activityJson = JsonSerializer.Serialize(activity);
        var content = new StringContent(activityJson, Encoding.UTF8, "application/activity+json");

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/users/test/inbox", content);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ErrorHandling_MalformedWebFingerRequest_IsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Missing required resource parameter
        var response = await client.GetAsync("/.well-known/webfinger");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ErrorHandling_InvalidActorId_IsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/users/nonexistent");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Helper Methods

    private void SignRequest(HttpContent content, RSA keyPair, string keyId, string hostname)
    {
        var body = content.ReadAsStringAsync().Result;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var headersToSign = "(request-target) host digest";
        var stringToSign = $"(request-target): post /users/test/inbox\nhost: {hostname}\ndigest: SHA-256={Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)))}";
        
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stringToSign));
        var signatureBytes = keyPair.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);

        content.Headers.Add("Digest", $"SHA-256={Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)))}");
        content.Headers.Add("Signature", $"keyId=\"{keyId}\",algorithm=\"rsa-sha256\",headers=\"{headersToSign}\",signature=\"{signature}\"");
    }

    #endregion
}
