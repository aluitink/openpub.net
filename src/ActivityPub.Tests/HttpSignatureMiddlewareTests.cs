using ActivityPub.Core.Middleware;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ActivityPub.Tests;

public class HttpSignatureMiddlewareTests
{
    [Fact]
    public async Task Verify_Expired_Signature_Is_Rejected()
    {
        // A 'created' timestamp outside the acceptable window is rejected with
        // 403 (replay protection) before any key lookup or signature check.
        var context = new DefaultHttpContext();

        var expiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 400;

        context.Request.Headers["Host"] = "example.com";
        context.Request.Headers["created"] = expiredTimestamp.ToString();
        context.Request.Headers["Authorization"] = "Signature keyId=\"test\",headers=\"(request-target)\",signature=\"fake\"";

        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";

        var services = new ServiceCollection();
        var mockKeyFetcher = new Mock<IKeyFetchingService>();
        services.AddSingleton(mockKeyFetcher.Object);
        context.RequestServices = services.BuildServiceProvider();

        var options = new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = false };
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            Mock.Of<ILogger<HttpSignatureMiddleware>>(),
            Options.Create(options));

        await middleware.InvokeAsync(context);

        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task Verify_Missing_Signature_Is_Rejected_When_Required()
    {
        // With RequireSignatures enabled, an unsigned inbox delivery is
        // rejected with 401.
        var context = new DefaultHttpContext();

        context.Request.Headers["Host"] = "example.com";
        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";

        var options = new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = true };
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            Mock.Of<ILogger<HttpSignatureMiddleware>>(),
            Options.Create(options));

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task Verify_Missing_Signature_Is_Tolerated_When_Not_Required()
    {
        // With RequireSignatures disabled (local-dev posture), an unsigned inbox
        // delivery is tolerated and passes through.
        var context = new DefaultHttpContext();

        context.Request.Headers["Host"] = "example.com";
        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";

        var options = new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = false };
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            Mock.Of<ILogger<HttpSignatureMiddleware>>(),
            Options.Create(options));

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.False(context.Response.HasStarted);
    }

    [Fact]
    public async Task Verify_NonInbox_Post_Bypasses_Verification()
    {
        // Signature enforcement only applies to inbox deliveries; other POST
        // paths pass through untouched.
        var context = new DefaultHttpContext();

        context.Request.Headers["Host"] = "example.com";
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/something";

        var options = new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = true };
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            Mock.Of<ILogger<HttpSignatureMiddleware>>(),
            Options.Create(options));

        await middleware.InvokeAsync(context);

        // Not an inbox path: no rejection, passes through.
        Assert.Equal(200, context.Response.StatusCode);
        Assert.False(context.Response.HasStarted);
    }

    [Fact]
    public async Task Verify_Disabled_Verification_Passes_Through()
    {
        // When verification is disabled entirely, even an unsigned inbox
        // delivery passes through.
        var context = new DefaultHttpContext();

        context.Request.Headers["Host"] = "example.com";
        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";

        var options = new ActivityPubOptions { EnableSignatureVerification = false, RequireSignatures = true };
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            Mock.Of<ILogger<HttpSignatureMiddleware>>(),
            Options.Create(options));

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.False(context.Response.HasStarted);
    }

    [Fact]
    public async Task Verify_SignedRequest_WithUnknownKeyId_Is_Rejected()
    {
        // A signed request whose keyId cannot be resolved to a public key is
        // rejected with 401.
        var context = new DefaultHttpContext();

        context.Request.Headers["Host"] = "example.com";
        context.Request.Headers["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        context.Request.Headers["Authorization"] = "Signature keyId=\"https://unknown.example/key\",headers=\"(request-target)\",signature=\"fake\"";
        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";

        var services = new ServiceCollection();
        var mockKeyFetcher = new Mock<IKeyFetchingService>();
        mockKeyFetcher.Setup(s => s.FetchPublicKeyAsync(It.IsAny<string>())).ReturnsAsync((PublicKey?)null);
        services.AddSingleton(mockKeyFetcher.Object);
        context.RequestServices = services.BuildServiceProvider();

        var options = new ActivityPubOptions { EnableSignatureVerification = true, RequireSignatures = false };
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            Mock.Of<ILogger<HttpSignatureMiddleware>>(),
            Options.Create(options));

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
    }
}
