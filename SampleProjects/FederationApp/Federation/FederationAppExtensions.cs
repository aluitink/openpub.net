using ActivityPub.Core;
using ActivityPub.Core.Options;
using FederationApp.Federation;
using Microsoft.EntityFrameworkCore;

namespace FederationApp;

public static class FederationAppExtensions
{
    public static WebApplicationBuilder AddFederationApp(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<ActivityPubDbContext>(options =>
            options.UseInMemoryDatabase("FederationApp"));

        builder.Services.AddScoped<InstanceManager>();
        builder.Services.AddScoped<FederationService>();
        builder.Services.AddHostedService<ActivityDeliveryService>();

        builder.Services.AddHttpClient("FederationClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return builder;
    }
}
