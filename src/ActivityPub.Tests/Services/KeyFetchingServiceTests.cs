using Microsoft.Extensions.Caching.Memory;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ActivityPub.Tests.Services;

public class KeyFetchingServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<KeyFetchingService> _logger;

    public KeyFetchingServiceTests()
    {
        var cacheOptions = new MemoryCacheOptions();
        _cache = new MemoryCache(cacheOptions);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<KeyFetchingService>();
    }

    [Fact]
    public async Task FetchPublicKeyAsync_NullKeyId_ReturnsNull()
    {
        var httpClient = new HttpClient();
        var service = new KeyFetchingService(httpClient, _cache, _logger);

        var result = await service.FetchPublicKeyAsync(null!);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchPublicKeyAsync_EmptyKeyId_ReturnsNull()
    {
        var httpClient = new HttpClient();
        var service = new KeyFetchingService(httpClient, _cache, _logger);

        var result = await service.FetchPublicKeyAsync(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchPublicKeyAsync_InvalidKeyId_ReturnsNull()
    {
        var httpClient = new HttpClient();
        var service = new KeyFetchingService(httpClient, _cache, _logger);

        var result = await service.FetchPublicKeyAsync("invalid-key-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchPublicKeyAsync_ValidKeyIdFromCache_ReturnsCachedKey()
    {
        var httpClient = new HttpClient();
        var service = new KeyFetchingService(httpClient, _cache, _logger);

        var cachedKey = new PublicKey
        {
            Id = "test-key-id",
            Owner = "https://example.com/user",
            PublicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...\n-----END PUBLIC KEY-----"
        };

        _cache.Set("test-key-id", cachedKey, TimeSpan.FromHours(1));

        var result = await service.FetchPublicKeyAsync("test-key-id");

        Assert.NotNull(result);
        Assert.Equal("test-key-id", result.Id);
        Assert.Equal("https://example.com/user", result.Owner);
    }

    [Fact]
    public async Task FetchPublicKeyAsync_CacheMiss_ReturnsNull()
    {
        var httpClient = new HttpClient();
        var service = new KeyFetchingService(httpClient, _cache, _logger);

        var result = await service.FetchPublicKeyAsync("non-existent-key-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchPublicKeyAsync_NullKeyId_ReturnsNull_Again()
    {
        var httpClient = new HttpClient();
        var service = new KeyFetchingService(httpClient, _cache, _logger);

        var result = await service.FetchPublicKeyAsync(null!);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchPublicKeyAsync_EmptyKeyId_ReturnsNull_Again()
    {
        var httpClient = new HttpClient();
        var service = new KeyFetchingService(httpClient, _cache, _logger);

        var result = await service.FetchPublicKeyAsync(string.Empty);

        Assert.Null(result);
    }
}
