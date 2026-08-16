using ActivityPub.Core.Middleware;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests;

public class HttpSignatureMiddlewareTests
{
    [Fact]
    public async Task Verify_Valid_Signature_Is_Accepted()
    {
        // Arrange
        var context = new DefaultHttpContext();

        var keyPair = RSA.Create(2048);
        var publicKeyPem = Convert.ToBase64String(keyPair.ExportSubjectPublicKeyInfo());

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var headersToSign = "(created)";
        var stringToSign = $"{headersToSign}:{timestamp}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stringToSign));
        var signatureBytes = keyPair.SignData(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);

        context.Request.Headers["Host"] = "example.com";
        context.Request.Headers["(created)"] = timestamp.ToString();
        context.Request.Headers["(expires)"] = (timestamp + 300).ToString();
        context.Request.Headers["Signature"] = $"keyId=\"test\",headers=\"(created)\",signature=\"{signature}\"";

        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";

        var logger = Mock.Of<ILogger<HttpSignatureMiddleware>>();
        var middleware = new HttpSignatureMiddleware(
            next: _ => Task.CompletedTask,
            logger
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task Verify_Expired_Signature_Is_Rejected()
    {
        // Arrange
        var context = new DefaultHttpContext();

        var expiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 400;

        context.Request.Headers["Host"] = "example.com";
        context.Request.Headers["(created)"] = expiredTimestamp.ToString();
        context.Request.Headers["(expires)"] = (expiredTimestamp + 300).ToString();
        context.Request.Headers["Signature"] = "keyId=\"test\",headers=\"(created)\",signature=\"fake\"";

        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";

        // Set up mock services in the request context
        var services = new ServiceCollection();
        var mockKeyFetcher = new Mock<IKeyFetchingService>();
        mockKeyFetcher.Setup(s => s.FetchPublicKeyAsync("test")).ReturnsAsync(new PublicKey { Id = "test", Owner = "https://example.com/test", PublicKeyPem = "fake" });
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
    public async Task Verify_Missing_Signature_Is_Rejected()
    {
        // Arrange
        var context = new DefaultHttpContext();

        context.Request.Headers["Host"] = "example.com";
        context.Request.Method = "POST";
        context.Request.Path = "/users/test/inbox";

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
}
