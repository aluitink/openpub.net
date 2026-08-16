using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace ActivityPub.Core.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public bool DisableBackgroundServices { get; set; }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);

        if (DisableBackgroundServices)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(IHostedService));
            });
        }

        return base.CreateHost(builder);
    }
}
