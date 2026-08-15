using ActivityPub.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace ActivityPub.Tests.DependencyInjection;

public class DependencyInjectionTests
{
    [Fact]
    public void AddActivityPub_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddActivityPub();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ActivityPub.Core.Interfaces.IActivityPubRepository>());
        Assert.NotNull(provider.GetRequiredService<ActivityPub.Core.Services.IKeyFetchingService>());
        Assert.NotNull(provider.GetRequiredService<ActivityPub.Core.Interfaces.IKeyGenerationService>());
        Assert.NotNull(provider.GetRequiredService<ActivityPub.Core.Services.IFederationDiscoveryService>());
        Assert.NotNull(provider.GetRequiredService<ActivityPub.Core.Services.IOutboundSigningService>());
        Assert.NotNull(provider.GetRequiredService<ActivityPub.Core.Services.IOutboundActivityService>());
        Assert.NotNull(provider.GetRequiredService<ActivityPub.Core.Services.IActivityValidationService>());
        Assert.NotNull(provider.GetRequiredService<ActivityPub.Core.Services.ISharedInboxService>());
        Assert.NotNull(provider.GetRequiredService<ActivityPub.Core.Caching.IFederationCache>());
    }

    [Fact]
    public void AddActivityPub_RegistersScopedLifetime()
    {
        var services = new ServiceCollection();
        services.AddActivityPub();

        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var repo1 = scope1.ServiceProvider.GetRequiredService<ActivityPub.Core.Interfaces.IActivityPubRepository>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<ActivityPub.Core.Interfaces.IActivityPubRepository>();

        Assert.NotNull(repo1);
        Assert.NotNull(repo2);
    }

    [Fact]
    public void AddActivityPub_RegistersHttpClient()
    {
        var services = new ServiceCollection();
        services.AddActivityPub();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<HttpClient>());
    }

    [Fact]
    public void AddActivityPub_RegistersMemoryCache()
    {
        var services = new ServiceCollection();
        services.AddActivityPub();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IMemoryCache>());
    }

    [Fact]
    public void AddActivityPub_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddActivityPub(options =>
        {
            options.Domain = "test.example.com";
            options.UserPath = "/users";
        });

        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ActivityPub.Core.Options.ActivityPubOptions>>();

        Assert.Equal("test.example.com", options.Value.Domain);
        Assert.Equal("/users", options.Value.UserPath);
    }
}
