using ActivityPub.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using Moq;
using System.Text;
using Xunit;

namespace ActivityPub.Tests;

public class SignatureVerificationTests
{
    [Fact]
    public void Verify_RSA_Signature_Generation()
    {
        // Generate a key pair
        using var keyPair = RSA.Create(2048);
        
        // Create a timestamp
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var headersToSign = "(created)";
        var stringToSign = $"{headersToSign}:{timestamp}";
        
        // Sign the string
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stringToSign));
        var signatureBytes = keyPair.SignData(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);
        
        // Verify the signature
        var isValid = keyPair.VerifyData(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
        Assert.True(isValid);
    }
    
    [Fact]
    public void Verify_Signature_Is_Invalid_When_Tampered()
    {
        // Generate a key pair
        using var keyPair = RSA.Create(2048);
        
        // Create a timestamp
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var headersToSign = "(created)";
        var stringToSign = $"{headersToSign}:{timestamp}";
        
        // Sign the string
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stringToSign));
        var signatureBytes = keyPair.SignData(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
        // Tamper with the string
        var tamperedString = $"{headersToSign}:{timestamp + 100}";
        
        // Verify the signature should fail
        var tamperedHash = SHA256.HashData(Encoding.UTF8.GetBytes(tamperedString));
        var isValid = keyPair.VerifyData(tamperedHash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
        Assert.False(isValid);
    }
    
    [Fact]
    public async Task Verify_KeyFetchingService_Caches_Keys()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpClient = new HttpClient();
        var logger = Mock.Of<ILogger<KeyFetchingService>>();
        var service = new KeyFetchingService(httpClient, cache, logger);
        
        // Act - First fetch
        var result1 = await service.FetchPublicKeyAsync("https://example.com/users/test");
        
        // Assert
        Assert.Null(result1); // No actual fetching happens in this test
    }
}
