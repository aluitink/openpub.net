using System.Diagnostics;
using System.Security.Cryptography;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActivityPub.Tests.Performance;

public class PerformanceTests
{
    private readonly IMemoryCache _cache;
    private readonly IKeyGenerationService _keyGenerationService;

    public PerformanceTests()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddScoped<IKeyGenerationService, KeyGenerationService>();
        var provider = services.BuildServiceProvider();

        _cache = provider.GetRequiredService<IMemoryCache>();
        _keyGenerationService = provider.GetRequiredService<IKeyGenerationService>();
    }

    [Fact]
    public void MemoryCache_SetAndGet_Benchmark()
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 1000; i++)
        {
            _cache.Set($"key:{i}", $"value:{i}");
            _cache.Get($"key:{i}");
        }

        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Memory cache operations too slow: {sw.ElapsedMilliseconds}ms for 2000 operations");
    }

    [Fact]
    public void KeyGeneration_Benchmark()
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            var (privateKey, publicKey) = _keyGenerationService.GenerateRSAKeyPair();
        }

        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 15000,
            $"Key generation too slow: {sw.ElapsedMilliseconds}ms for 100 key pairs");
    }
}
