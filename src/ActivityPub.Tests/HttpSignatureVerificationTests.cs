using System.Net;
using System.Security.Cryptography;
using System.Text;
using ActivityPub.Core.Middleware;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ActivityPub.Tests;

/// <summary>
/// Tests for HTTP signature verification of inbound ActivityPub activity
/// deliveries. The centerpiece is a round-trip: the production
/// <see cref="OutboundSigningService"/> signs a request, the equivalent
/// <see cref="HttpContext"/> is reconstructed, and the
/// <see cref="HttpSignatureMiddleware"/> must accept it. Negative cases
/// (tampered body, wrong key, expired, missing signature under strict mode)
/// must be rejected.
/// </summary>
public class HttpSignatureVerificationTests
{
    private const string InboxPath = "/users/remoteactor/inbox";
    private const string KeyId = "https://remote.example/users/remoteactor#main-key";
    private const string Host = "remote.example";

    /// <summary>Holds mutable test state shared between the middleware and assertions.</summary>
    private sealed class TestState
    {
        public HttpSignatureMiddleware Middleware { get; set; } = null!;
        public IServiceProvider Services { get; set; } = null!;
        public bool NextCalled { get; set; }
    }

    private static (RSA Rsa, string PrivateKeyPem, string PublicKeyPem) CreateKeyPair()
    {
        var rsa = RSA.Create(2048);
        return (rsa, rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
    }

    private static TestState CreateMiddleware(ActivityPubOptions options, string publicKeyPem, string keyId = KeyId)
    {
        var state = new TestState();
        var logger = Mock.Of<ILogger<HttpSignatureMiddleware>>();
        state.Middleware = new HttpSignatureMiddleware(
            next: ctx => { state.NextCalled = true; return Task.CompletedTask; },
            logger,
            Options.Create(options));

        var services = new ServiceCollection();
        var keyFetcher = new Mock<IKeyFetchingService>();
        keyFetcher
            .Setup(s => s.FetchPublicKeyAsync(keyId))
            .ReturnsAsync(new PublicKey { Id = keyId, Owner = "https://remote.example/users/remoteactor", PublicKeyPem = publicKeyPem });
        services.AddSingleton(keyFetcher.Object);
        state.Services = services.BuildServiceProvider();
        return state;
    }

    /// <summary>
    /// Signs an ActivityPub inbox POST exactly like the production signer does,
    /// then builds the matching HttpContext for the verifier.
    /// </summary>
    private static HttpContext BuildSignedContext(string privateKeyPem, string body, long? createdOverride = null, string? bodyToDigest = null)
    {
        var bodyToSign = bodyToDigest ?? body;
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = InboxPath;
        context.Request.Headers["Host"] = Host;
        context.Request.ContentType = "application/activity+json";

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.EnableBuffering();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var created = createdOverride ?? now;

        // Mirror the inbound request as an HttpRequestMessage so the real
        // OutboundSigningService signs the same bytes the verifier will see.
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://{Host}{InboxPath}");
        request.Content = new StringContent(bodyToSign, Encoding.UTF8, "application/activity+json");
        request.Headers.Date = DateTimeOffset.FromUnixTimeSeconds(created).UtcDateTime;

        var signer = new OutboundSigningService(Mock.Of<ILogger<OutboundSigningService>>());
        signer.SignRequest(request, privateKeyPem, KeyId, Host);

        // Copy the produced headers onto the context.
        context.Request.Headers["Date"] = request.Headers.Date?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (request.Headers.TryGetValues("Digest", out var digestValues))
        {
            context.Request.Headers["Digest"] = digestValues.First();
        }

        var auth = request.Headers.Authorization;
        Assert.NotNull(auth);
        context.Request.Headers["Authorization"] = $"{auth.Scheme} {auth.Parameter}";

        context.Request.Headers["created"] = created.ToString();
        return context;
    }

    [Fact]
    public async Task RoundTrip_ValidSignature_FromProductionSigner_IsAccepted()
    {
        var (_, privateKeyPem, publicKeyPem) = CreateKeyPair();
        var state = CreateMiddleware(new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = false }, publicKeyPem);

        var body = """{"@context":"https://www.w3.org/ns/activitystreams","type":"Create","actor":"https://remote.example/users/remoteactor"}""";
        var context = BuildSignedContext(privateKeyPem, body);
        context.RequestServices = state.Services;

        await state.Middleware.InvokeAsync(context);

        Assert.True(state.NextCalled, "The validly signed request should pass through to the next middleware");
        Assert.False(context.Response.HasStarted, "The middleware should not have written a rejection response");
    }

    [Fact]
    public async Task RoundTrip_TamperedBody_IsRejected()
    {
        var (_, privateKeyPem, publicKeyPem) = CreateKeyPair();
        var state = CreateMiddleware(new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = false }, publicKeyPem);

        // Sign one body, then present a *different* body. The digest (covered
        // in the signature) no longer matches the body.
        var original = """{"type":"Create","content":"hello"}""";
        var tampered = """{"type":"Create","content":"evil"}""";
        var context = BuildSignedContext(privateKeyPem, tampered, bodyToDigest: original);
        context.RequestServices = state.Services;

        await state.Middleware.InvokeAsync(context);

        Assert.False(state.NextCalled, "A tampered request must not pass through");
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task RoundTrip_WrongKey_IsRejected()
    {
        var (_, privateKeyPem, _) = CreateKeyPair();
        var (_, _, wrongPublicKeyPem) = CreateKeyPair();
        var state = CreateMiddleware(new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = false }, wrongPublicKeyPem);

        var body = """{"type":"Create","content":"hello"}""";
        var context = BuildSignedContext(privateKeyPem, body);
        context.RequestServices = state.Services;

        await state.Middleware.InvokeAsync(context);

        Assert.False(state.NextCalled, "A signature that does not match the key must be rejected");
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task ExpiredSignature_IsRejected()
    {
        var (_, privateKeyPem, publicKeyPem) = CreateKeyPair();
        var state = CreateMiddleware(new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = false }, publicKeyPem);

        // 'created' 400s in the past is outside the 300s window.
        var expired = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 400;
        var body = """{"type":"Create","content":"hello"}""";
        var context = BuildSignedContext(privateKeyPem, body, createdOverride: expired);
        context.RequestServices = state.Services;

        await state.Middleware.InvokeAsync(context);

        Assert.False(state.NextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task Unsigned_IsTolerated_WhenRequireSignaturesDisabled()
    {
        var (_, _, publicKeyPem) = CreateKeyPair();
        var state = CreateMiddleware(new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = false }, publicKeyPem);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = InboxPath;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.RequestServices = state.Services;

        await state.Middleware.InvokeAsync(context);

        Assert.True(state.NextCalled, "Unsigned requests must pass through when RequireSignatures is disabled");
        Assert.False(context.Response.HasStarted);
    }

    [Fact]
    public async Task Unsigned_IsRejected_WhenRequireSignaturesEnabled()
    {
        var (_, _, publicKeyPem) = CreateKeyPair();
        var state = CreateMiddleware(new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = true }, publicKeyPem);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = InboxPath;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.RequestServices = state.Services;

        await state.Middleware.InvokeAsync(context);

        Assert.False(state.NextCalled, "Unsigned requests must be rejected when RequireSignatures is enabled");
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task VerificationDisabled_PassesThroughEvenWhenUnsigned()
    {
        var (_, _, publicKeyPem) = CreateKeyPair();
        var state = CreateMiddleware(new ActivityPubOptions { EnableSignatureVerification = false, RequireSignatures = true }, publicKeyPem);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = InboxPath;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.RequestServices = state.Services;

        await state.Middleware.InvokeAsync(context);

        Assert.True(state.NextCalled, "When verification is disabled, all inbox POSTs pass through");
        Assert.False(context.Response.HasStarted);
    }

    [Fact]
    public async Task NonInboxPath_IsIgnored()
    {
        var (_, _, publicKeyPem) = CreateKeyPair();
        var state = CreateMiddleware(new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = true }, publicKeyPem);

        // A POST to a non-inbox path must not be subject to verification, even
        // in strict mode.
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/users/remoteactor/outbox";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.RequestServices = state.Services;

        await state.Middleware.InvokeAsync(context);

        Assert.True(state.NextCalled, "Non-inbox POSTs must pass through unverified");
        Assert.False(context.Response.HasStarted);
    }

    [Fact]
    public async Task MissingKeyId_IsRejected()
    {
        var (_, _, publicKeyPem) = CreateKeyPair();
        var state = CreateMiddleware(new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = false }, publicKeyPem);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = InboxPath;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        // Signature header present but with no keyId.
        context.Request.Headers["Signature"] = $"headers=\"(request-target) created\",created=\"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\",signature=\"YWJj\"";
        context.RequestServices = state.Services;

        await state.Middleware.InvokeAsync(context);

        Assert.False(state.NextCalled);
        Assert.Equal(401, context.Response.StatusCode);
    }

    /// <summary>
    /// KeyFetchingService fetches the keyId's base URL directly (the actor
    /// JSON-LD document) rather than appending .jsonld/.json.
    /// </summary>
    [Fact]
    public async Task KeyFetching_FetchesActorDocumentAtKeyIdBaseUrl()
    {
        var handler = new CapturingHttpMessageHandler(
            // The public key is nested under the actor's "publicKey" property,
            // per the ActivityPub actor document shape.
            responseBody: """{"id":"https://remote.example/users/remoteactor","publicKey":{"id":"https://remote.example/users/remoteactor#main-key","owner":"https://remote.example/users/remoteactor","publicKeyPem":"-----BEGIN PUBLIC KEY-----abc-----END PUBLIC KEY-----"}}""");

        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new KeyFetchingService(httpClient, cache, Mock.Of<ILogger<KeyFetchingService>>());

        var key = await service.FetchPublicKeyAsync("https://remote.example/users/remoteactor#main-key");

        Assert.NotNull(key);
        Assert.Equal("-----BEGIN PUBLIC KEY-----abc-----END PUBLIC KEY-----", key!.PublicKeyPem);
        // It must have hit the base URL directly, not a .jsonld/.json variant.
        Assert.Equal("https://remote.example/users/remoteactor", handler.LastRequestUrl);
    }

    // ---- Test helpers -----------------------------------------------------

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public CapturingHttpMessageHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        public string? LastRequestUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUrl = request.RequestUri?.ToString();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/activity+json")
            };
            return Task.FromResult(response);
        }
    }
}
