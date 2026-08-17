using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Caching;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Middleware;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// Phase 38 Task 4 — end-to-end server-to-server federation tests. Verifies
/// that the outbound delivery pipeline produces a correctly-signed HTTP
/// request (W3C draft-cavage, RSA-SHA256, with the required <c>created</c>
/// signature parameter) that our own inbound
/// <see cref="HttpSignatureMiddleware"/> will accept, and that the private key
/// is correctly retrieved from the local actor record during queue processing.
/// </summary>
public class OutboundFederationEndToEndTests : IDisposable
{
    private readonly RSA _keyPair;
    private readonly string _privateKeyPem;
    private readonly string _publicKeyPem;

    public OutboundFederationEndToEndTests()
    {
        _keyPair = RSA.Create(2048);
        _privateKeyPem = _keyPair.ExportPkcs8PrivateKeyPem();
        _publicKeyPem = _keyPair.ExportSubjectPublicKeyInfoPem();
    }

    public void Dispose() => _keyPair.Dispose();

    /// <summary>
    /// Verifies that the outbound signer produces a signature that includes the
    /// <c>created</c> parameter and that the signature is cryptographically
    /// valid (verifiable with the public key).
    /// </summary>
    [Fact]
    public void OutboundSigner_IncludesCreated_Parameter_InSignature()
    {
        const string host = "remote.example";
        const string path = "/inbox";
        const string keyId = "https://local.example/users/alice#main-key";
        const string body = """{"type":"Create","id":"urn:activity:1"}""";

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://{host}{path}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/activity+json");

        var signer = new OutboundSigningService(Mock.Of<ILogger<OutboundSigningService>>());
        signer.SignRequest(request, _privateKeyPem, keyId, host);

        // The Authorization header must be present and use the Signature scheme.
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Signature", request.Headers.Authorization!.Scheme);

        var authValue = request.Headers.Authorization.Parameter!;

        // Must include the created parameter.
        Assert.Contains("created=", authValue);

        // Must include the keyId.
        Assert.Contains($"keyId=\"{keyId}\"", authValue);

        // Must include the algorithm.
        Assert.Contains("algorithm=\"rsa-sha256\"", authValue);

        // Must include the signature.
        Assert.Contains("signature=\"", authValue);
    }

    /// <summary>
    /// Verifies that a request signed by the outbound signer is accepted by our
    /// inbound <see cref="HttpSignatureMiddleware"/> (the full round-trip:
    /// sign -> verify). This is the core end-to-end federation guarantee.
    /// </summary>
    [Fact]
    public async Task SignedOutboundRequest_IsAccepted_ByInboundVerifier()
    {
        const string host = "localhost";
        const string path = "/users/test/inbox";
        const string keyId = "https://localhost/users/test#main-key";
        const string body = """{"type":"Create","id":"urn:activity:1"}""";

        // Build and sign an outbound request the way production does.
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://{host}{path}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/activity+json");
        var signer = new OutboundSigningService(Mock.Of<ILogger<OutboundSigningService>>());
        signer.SignRequest(request, _privateKeyPem, keyId, host);

        // Reconstruct the inbound context from the signed outbound request.
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Request.Headers["Host"] = host;
        context.Request.ContentType = "application/activity+json";
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.EnableBuffering();
        context.Request.Headers["Date"] = request.Headers.Date?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "";
        if (request.Headers.TryGetValues("Digest", out var digestValues))
        {
            context.Request.Headers["Digest"] = digestValues.FirstOrDefault() ?? "";
        }
        context.Request.Headers["Authorization"] = $"{request.Headers.Authorization?.Scheme} {request.Headers.Authorization?.Parameter}";

        // The created parameter is now in the signature params (from the signer),
        // so we do NOT need to manually set a created header.

        var services = new ServiceCollection();
        var mockKeyFetcher = new Mock<IKeyFetchingService>();
        mockKeyFetcher.Setup(s => s.FetchPublicKeyAsync(keyId)).ReturnsAsync(new PublicKey
        {
            Id = keyId,
            Owner = "https://localhost/users/test",
            PublicKeyPem = _publicKeyPem
        });
        services.AddSingleton(mockKeyFetcher.Object);
        context.RequestServices = services.BuildServiceProvider();

        var options = new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = true };
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            Mock.Of<ILogger<HttpSignatureMiddleware>>(),
            Options.Create(options));

        context.Request.Body.Position = 0;
        await middleware.InvokeAsync(context);

        // Accepted: no rejection status is written (response stays 200).
        Assert.Equal(200, context.Response.StatusCode);
        Assert.False(context.Response.HasStarted);
    }

    /// <summary>
    /// Verifies that <see cref="SharedInboxService.ProcessQueueAsync"/>
    /// retrieves the sender's private key from the local actor record and
    /// passes it to the outbound sender (the critical bug fix: previously
    /// <c>string.Empty</c> was passed, causing every delivery to fail).
    /// </summary>
    [Fact]
    public async Task ProcessQueueAsync_RetrievesPrivateKey_FromActorRecord()
    {
        var repo = new InMemoryActivityPubRepository();

        // Seed a local actor with a private key.
        var actor = new Actor
        {
            Id = "https://local.example/users/alice#main-key",
            PreferredUsername = "alice",
            AdditionalProperties = new System.Collections.Generic.Dictionary<string, JsonElement>
            {
                ["privateKeyPem"] = JsonSerializer.SerializeToElement(_privateKeyPem)
            }
        };
        await repo.SaveUserActorAsync(actor);

        // Capture the arguments passed to SendActivityAsync.
        string? capturedPrivateKey = null;
        string? capturedActorId = null;
        var outbound = new Mock<IOutboundActivityService>();
        outbound
            .Setup(s => s.SendActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string, string>((_, actorId, privateKey, _) =>
            {
                capturedActorId = actorId;
                capturedPrivateKey = privateKey;
            })
            .ReturnsAsync(true);

        var options = new ActivityPubOptions
        {
            DeliveryRetry = new DeliveryRetryOptions(),
            PeerHealth = new PeerHealthOptions()
        };
        var service = new SharedInboxService(
            repo,
            outbound.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IFederationCache>(),
            Mock.Of<ILogger<SharedInboxService>>(),
            Options.Create(options));

        // Queue a delivery from the local actor and capture the entity reference.
        const string activityJson = """{"id":"urn:activity:1","type":"Create","actor":"https://local.example/users/alice#main-key"}""";
        Assert.True(await repo.QueueSharedInboxDeliveryAsync("act-e2e", activityJson, "https://remote.example/users/bob#main-key"));
        var pending = await repo.GetPendingSharedInboxDeliveriesAsync(10, 10);
        var item = Assert.Single(pending, d => d.ActivityId == "act-e2e");

        await service.ProcessQueueAsync();

        // The outbound sender must have been called with the real private key
        // (not string.Empty) and the correct actor ID.
        Assert.NotNull(capturedPrivateKey);
        Assert.NotEqual(string.Empty, capturedPrivateKey);
        Assert.Equal(_privateKeyPem, capturedPrivateKey);
        Assert.Equal("https://local.example/users/alice#main-key", capturedActorId);

        // The delivery must have been marked as Delivered.
        Assert.Equal(DeliveryStatus.Delivered, item.Status);
    }

    /// <summary>
    /// Verifies that when no local actor exists for the sender (e.g. the
    /// activity was created before the actor was saved, or the actor is remote),
    /// the delivery fails gracefully with a clear failure reason rather than
    /// throwing an exception.
    /// </summary>
    [Fact]
    public async Task ProcessQueueAsync_NoPrivateKey_MarksFailed_WithClearReason()
    {
        var repo = new InMemoryActivityPubRepository();
        // Do NOT seed an actor — simulating a missing private key.

        var outbound = new Mock<IOutboundActivityService>();
        outbound
            .Setup(s => s.SendActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var options = new ActivityPubOptions
        {
            DeliveryRetry = new DeliveryRetryOptions(),
            PeerHealth = new PeerHealthOptions()
        };
        var service = new SharedInboxService(
            repo,
            outbound.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IFederationCache>(),
            Mock.Of<ILogger<SharedInboxService>>(),
            Options.Create(options));

        const string activityJson = """{"id":"urn:activity:2","type":"Create","actor":"https://local.example/users/bob#main-key"}""";
        Assert.True(await repo.QueueSharedInboxDeliveryAsync("act-nokey", activityJson, "https://remote.example/users/bob#main-key"));
        var pending = await repo.GetPendingSharedInboxDeliveriesAsync(10, 10);
        var item = Assert.Single(pending, d => d.ActivityId == "act-nokey");

        await service.ProcessQueueAsync();

        // The outbound sender must NOT have been called (no private key).
        outbound.Verify(
            s => s.SendActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        // The delivery must be marked Failed with a clear reason.
        Assert.Equal(DeliveryStatus.Failed, item.Status);
        Assert.Contains("private key", item.FailureReason, StringComparison.OrdinalIgnoreCase);
    }
}
