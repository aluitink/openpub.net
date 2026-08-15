using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ActivityPub.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace ActivityPub.Tests.Concurrency;

public class ConcurrencyTests
{
    private readonly IMemoryCache _cache;

    public ConcurrencyTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task MemoryCache_ConcurrentSet_Get()
    {
        var tasks = new List<Task>();
        var errors = new ConcurrentBag<Exception>();
        var successCount = 0;
        var lockObj = new object();

        for (int i = 0; i < 10; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var key = $"concurrent:{threadId}:{j}";
                        _cache.Set(key, $"value:{threadId}-{j}");
                        var result = _cache.Get<string>(key);
                        
                        if (result != null)
                        {
                            lock (lockObj)
                            {
                                Interlocked.Increment(ref successCount);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        
        Assert.Empty(errors);
        Assert.Equal(1000, successCount);
    }

    [Fact]
    public async Task MemoryCache_ConcurrentRemove()
    {
        var tasks = new List<Task>();
        var errors = new ConcurrentBag<Exception>();

        for (int i = 0; i < 100; i++)
        {
            _cache.Set($"remove-test:{i}", $"value:{i}");
        }

        for (int i = 0; i < 10; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    for (int j = threadId * 10; j < (threadId + 1) * 10; j++)
                    {
                        _cache.Remove($"remove-test:{j}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        
        Assert.Empty(errors);
    }
}
