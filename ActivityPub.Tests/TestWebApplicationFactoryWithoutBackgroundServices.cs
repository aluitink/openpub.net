using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ActivityPub.Core.Tests;

public class TestWebApplicationFactoryWithoutBackgroundServices : TestWebApplicationFactory
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        
        // Disable background services
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IHostedService));
        });
        
        return base.CreateHost(builder);
    }
}
