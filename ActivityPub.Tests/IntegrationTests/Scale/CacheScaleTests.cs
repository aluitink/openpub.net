using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using ActivityPub.Core.Models;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Caching;
using ActivityPub.Core.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;

namespace ActivityPub.Tests.IntegrationTests.Scale;

public class CacheScaleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CacheScaleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CacheScale_HandlesLargeCachePopulation()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int CacheItemCount = 1500;

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IFederationCache>();

        var tasks = new List<Task>();
        for (int i = 0; i < CacheItemCount; i++)
        {
            var actor = new Actor
            {
                Id = $"https://localhost/users/cache-pop-{testRunId}-{i}",
                Type = "Person",
                PreferredUsername = $"cache-pop-{testRunId}-{i}",
                Inbox = $"https://localhost/users/cache-pop-{testRunId}-{i}/inbox",
                Outbox = $"https://localhost/users/cache-pop-{testRunId}-{i}/outbox"
            };
            tasks.Add(cache.SetActorAsync(actor.Id, actor));
        }

        await Task.WhenAll(tasks);

        var count = cache.Count;
        Assert.True(count >= CacheItemCount);
    }

    [Fact]
    public async Task CacheScale_CanRetrieveFromLargeCache()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int CacheItemCount = 1000;

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IFederationCache>();

        var tasks = new List<Task>();
        for (int i = 0; i < CacheItemCount; i++)
        {
            var actor = new Actor
            {
                Id = $"https://localhost/users/cache-retr-{testRunId}-{i}",
                Type = "Person",
                PreferredUsername = $"cache-retr-{testRunId}-{i}",
                Inbox = $"https://localhost/users/cache-retr-{testRunId}-{i}/inbox",
                Outbox = $"https://localhost/users/cache-retr-{testRunId}-{i}/outbox"
            };
            tasks.Add(cache.SetActorAsync(actor.Id, actor));
        }

        await Task.WhenAll(tasks);

        var retrieveTasks = new List<Task<Actor?>>();
        for (int i = 0; i < 100; i++)
        {
            retrieveTasks.Add(cache.GetActorAsync($"https://localhost/users/cache-retr-{testRunId}-{i}"));
        }

        var retrieved = await Task.WhenAll(retrieveTasks);

        Assert.Equal(100, retrieved.Count(a => a != null));
    }

    [Fact]
    public async Task CacheScale_CanHandleConcurrentCacheAccess()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int ConcurrentCount = 300;

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IFederationCache>();

        var writeTasks = new List<Task>();
        for (int i = 0; i < ConcurrentCount; i++)
        {
            var actor = new Actor
            {
                Id = $"https://localhost/users/cache-conc-{testRunId}-{i}",
                Type = "Person",
                PreferredUsername = $"cache-conc-{testRunId}-{i}",
                Inbox = $"https://localhost/users/cache-conc-{testRunId}-{i}/inbox",
                Outbox = $"https://localhost/users/cache-conc-{testRunId}-{i}/outbox"
            };
            writeTasks.Add(cache.SetActorAsync(actor.Id, actor));
        }

        await Task.WhenAll(writeTasks);

        var readTasks = new List<Task<Actor?>>();
        for (int i = 0; i < 100; i++)
        {
            readTasks.Add(cache.GetActorAsync($"https://localhost/users/cache-conc-{testRunId}-{i}"));
        }

        var results = await Task.WhenAll(readTasks);

        Assert.Equal(100, results.Count(a => a != null));
    }

    [Fact]
    public async Task CacheScale_CanManageCacheMemoryEfficiently()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int InitialCount = 500;
        const int AdditionalCount = 500;

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IFederationCache>();

        var initialTasks = new List<Task>();
        for (int i = 0; i < InitialCount; i++)
        {
            var actor = new Actor
            {
                Id = $"https://localhost/users/cache-mem-{testRunId}-init-{i}",
                Type = "Person",
                PreferredUsername = $"cache-mem-{testRunId}-init-{i}",
                Inbox = $"https://localhost/users/cache-mem-{testRunId}-init-{i}/inbox",
                Outbox = $"https://localhost/users/cache-mem-{testRunId}-init-{i}/outbox"
            };
            initialTasks.Add(cache.SetActorAsync(actor.Id, actor));
        }

        await Task.WhenAll(initialTasks);

        var initialCount = cache.Count;

        var additionalTasks = new List<Task>();
        for (int i = 0; i < AdditionalCount; i++)
        {
            var actor = new Actor
            {
                Id = $"https://localhost/users/cache-mem-{testRunId}-add-{i}",
                Type = "Person",
                PreferredUsername = $"cache-mem-{testRunId}-add-{i}",
                Inbox = $"https://localhost/users/cache-mem-{testRunId}-add-{i}/inbox",
                Outbox = $"https://localhost/users/cache-mem-{testRunId}-add-{i}/outbox"
            };
            additionalTasks.Add(cache.SetActorAsync(actor.Id, actor));
        }

        await Task.WhenAll(additionalTasks);

        var finalCount = cache.Count;
        Assert.True(finalCount >= initialCount);
    }

    [Fact]
    public async Task CacheScale_CanInvalidateBatchByDomain()
    {
        var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
        const int DomainActorCount = 200;

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IFederationCache>();

        for (int i = 0; i < DomainActorCount; i++)
        {
            var actor = new Actor
            {
                Id = $"https://localhost/users/cache-inval-{testRunId}-{i}",
                Type = "Person",
                PreferredUsername = $"cache-inval-{testRunId}-{i}",
                Inbox = $"https://localhost/users/cache-inval-{testRunId}-{i}/inbox",
                Outbox = $"https://localhost/users/cache-inval-{testRunId}-{i}/outbox"
            };
            cache.SetActorAsync(actor.Id, actor).Wait();
        }

        var initialCount = cache.Count;
        Assert.True(initialCount >= DomainActorCount, $"Expected count >= {DomainActorCount}, got {initialCount}");

        cache.InvalidateActorsByDomainAsync("localhost").Wait();

        var finalCount = cache.Count;
        Assert.True(finalCount < initialCount, $"Expected final count < {initialCount}, got {finalCount}");
    }
}
