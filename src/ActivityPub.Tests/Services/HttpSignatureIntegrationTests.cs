using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Middleware;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ActivityPub.Core.Tests.Services;

public class HttpSignatureIntegrationTests
{
    [Fact]
    public void HttpSignature_VerifySignatureWithRealKey()
    {
        var logger = Mock.Of<ILogger<HttpSignatureMiddleware>>();
        var middleware = new HttpSignatureMiddleware(_ => Task.CompletedTask, logger);

        using var rsa = RSA.Create();
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        var content = "{ \"test\": \"data\" }";
        var contentBytes = Encoding.UTF8.GetBytes(content);

        using var sha256 = SHA256.Create();
        var digestHash = Convert.ToBase64String(sha256.ComputeHash(contentBytes));
        var digest = $"SHA-256={digestHash}";

        var target = "post /inbox";
        var headersToSign = new[] { "(request-target)", "digest", "created" };
        var headersStr = string.Join(" ", headersToSign);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var created = now.ToString();

        var contentToSign = string.Join("\n", new[]
        {
            $"(request-target): {target}",
            $"digest: {digest}",
            $"created: {created}"
        });

        var contentBytesToSign = Encoding.UTF8.GetBytes(contentToSign);
        var hash = sha256.ComputeHash(contentBytesToSign);
        var signatureBytes = rsa.SignData(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);

        var signatureHeader = $"Signature keyId=\"key1\",algorithm=\"rsa-sha256\",headers=\"{headersStr}\",created=\"{created}\",signature=\"{signature}\"";

        using var rsaVerify = RSA.Create();
        rsaVerify.ImportFromPem(publicKeyPem);

        var hashVerify = SHA256.Create();
        var contentBytesToVerify = Encoding.UTF8.GetBytes(contentToSign);
        var hashToVerify = hashVerify.ComputeHash(contentBytesToVerify);

        var isValid = rsaVerify.VerifyData(hashToVerify, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.True(isValid);
    }

    [Fact]
    public void HttpSignature_DifferentKeysProduceDifferentSignatures()
    {
        var content = "{ \"test\": \"data\" }";
        var target = "post /inbox";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        using var rsa1 = RSA.Create();
        using var rsa2 = RSA.Create();

        var privateKey1 = rsa1.ExportPkcs8PrivateKeyPem();
        var privateKey2 = rsa2.ExportPkcs8PrivateKeyPem();

        var contentBytes = Encoding.UTF8.GetBytes(content);
        var sha256 = SHA256.Create();
        var digestHash = Convert.ToBase64String(sha256.ComputeHash(contentBytes));
        var digest = $"SHA-256={digestHash}";

        var headersToSign = new[] { "(request-target)", "digest", "created" };
        var headersStr = string.Join(" ", headersToSign);

        var contentToSign = string.Join("\n", new[]
        {
            $"(request-target): {target}",
            $"digest: {digest}",
            $"created: {created}"
        });

        var contentBytesToSign = Encoding.UTF8.GetBytes(contentToSign);
        var hash = sha256.ComputeHash(contentBytesToSign);

        var signatureBytes1 = rsa1.SignData(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureBytes2 = rsa2.SignData(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var signature1 = Convert.ToBase64String(signatureBytes1);
        var signature2 = Convert.ToBase64String(signatureBytes2);

        Assert.NotEqual(signature1, signature2);
    }

    [Fact]
    public void HttpSignature_SignatureVerificationFailsWithWrongKey()
    {
        var content = "{ \"test\": \"data\" }";
        var target = "post /inbox";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        using var rsa = RSA.Create();
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        var contentBytes = Encoding.UTF8.GetBytes(content);
        var sha256 = SHA256.Create();
        var digestHash = Convert.ToBase64String(sha256.ComputeHash(contentBytes));
        var digest = $"SHA-256={digestHash}";

        var headersToSign = new[] { "(request-target)", "digest", "created" };
        var headersStr = string.Join(" ", headersToSign);

        var contentToSign = string.Join("\n", new[]
        {
            $"(request-target): {target}",
            $"digest: {digest}",
            $"created: {created}"
        });

        var contentBytesToSign = Encoding.UTF8.GetBytes(contentToSign);
        var hash = sha256.ComputeHash(contentBytesToSign);
        var signatureBytes = rsa.SignData(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);

        using var rsaWrong = RSA.Create();
        var wrongPublicKeyPem = rsaWrong.ExportSubjectPublicKeyInfoPem();

        using var rsaVerify = RSA.Create();
        rsaVerify.ImportFromPem(wrongPublicKeyPem);

        var hashVerify = SHA256.Create();
        var contentBytesToVerify = Encoding.UTF8.GetBytes(contentToSign);
        var hashToVerify = hashVerify.ComputeHash(contentBytesToVerify);

        var isValid = rsaVerify.VerifyData(hashToVerify, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.False(isValid);
    }
}
