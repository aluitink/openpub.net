using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ConsoleClient;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddActivityPub();
        builder.Services.AddHttpClient();

        var host = builder.Build();

        var keyService = host.Services.GetRequiredService<IKeyGenerationService>();
        var signingService = host.Services.GetRequiredService<IOutboundSigningService>();
        var activityService = host.Services.GetRequiredService<IOutboundActivityService>();
        var discoveryService = host.Services.GetRequiredService<IFederationDiscoveryService>();

        Console.WriteLine("ActivityPub Console Client");
        Console.WriteLine("==========================\n");

        while (true)
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  1. Generate Keys");
            Console.WriteLine("  2. Sign Activity");
            Console.WriteLine("  3. Discover Domain Endpoint");
            Console.WriteLine("  4. Exit");
            Console.Write("\nSelect option: ");

            var input = Console.ReadLine();
            if (input == "0" || input == null) break;

            switch (input)
            {
                case "1":
                    GenerateKeys(keyService);
                    break;
                case "2":
                    await SignActivityAsync(signingService, keyService);
                    break;
                case "3":
                    await DiscoverEndpointAsync(discoveryService);
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Invalid option");
                    break;
            }

            Console.WriteLine();
        }
    }

    static void GenerateKeys(IKeyGenerationService keyService)
    {
        var (privateKey, publicKey) = keyService.GenerateRSAKeyPair();
        Console.WriteLine("\nGenerated Keys:");
        Console.WriteLine($"\nPrivate Key:\n{privateKey}");
        Console.WriteLine($"\nPublic Key:\n{publicKey}");
    }

    static async Task SignActivityAsync(IOutboundSigningService signingService, IKeyGenerationService keyService)
    {
        var (privateKey, _) = keyService.GenerateRSAKeyPair();

        Console.Write("\nActor URL: ");
        var actor = Console.ReadLine() ?? "https://localhost/users/client";

        Console.Write("Activity JSON: ");
        var activityJson = Console.ReadLine() ?? "{}";

        Console.Write("Recipient inbox URL: ");
        var to = Console.ReadLine() ?? "https://remote.actor/inbox";

        var activity = new
        {
            @context = "https://www.w3.org/ns/activitystreams",
            id = $"https://localhost/activities/{Guid.NewGuid()}",
            type = "Follow",
            actor = actor,
            @object = "https://remote.actor/users/other",
            to = new[] { "https://www.w3.org/ns/activitystreams#Public" }
        };

        var json = JsonSerializer.Serialize(activity, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Console.WriteLine($"\nActivity to send:\n{json}");

        var recipientUri = new Uri(to);
        var domain = recipientUri.Host;
        var keyId = $"{actor}#main-key";

        var request = new HttpRequestMessage(HttpMethod.Post, to);
        request.Content = new StringContent(json, Encoding.UTF8, "application/activity+json");

        signingService.SignRequest(request, privateKey, keyId, domain);

        Console.WriteLine($"\nSigned Request:");
        Console.WriteLine($"  Method: POST");
        Console.WriteLine($"  URL: {to}");
        Console.WriteLine($"  Authorization: {request.Headers.Authorization}");
    }

    static async Task DiscoverEndpointAsync(IFederationDiscoveryService discoveryService)
    {
        Console.Write("\nDomain: ");
        var domain = Console.ReadLine() ?? "example.com";

        Console.WriteLine($"\nDiscovering endpoint for: {domain}");

        var endpoint = await discoveryService.DiscoverEndpointAsync(domain);

        if (!string.IsNullOrEmpty(endpoint))
        {
            Console.WriteLine($"\nDiscovered Endpoint: {endpoint}");
            Console.WriteLine($"  Inbox: {endpoint}/inbox");
            Console.WriteLine($"  Outbox: {endpoint}/outbox");
        }
        else
        {
            Console.WriteLine("\nFailed to discover endpoint");
        }
    }
}
