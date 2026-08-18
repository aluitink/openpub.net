using System.Text.Json;
using ActivityPub.Core.Events;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ActivityPub.Tests.Services;

/// <summary>
/// LIVE federation round-trip against a real public instance (mastodon.world).
/// Runs the real outbound WebFingerService.ResolveActorAsync with a real
/// HttpClient against the real fediverse — this is the end-to-end proof that
/// the app can "communicate with the outside world."
///
/// Gated behind the "LiveFederation" category so it does NOT run in the default
/// `dotnet test` (which must stay green offline). Run it explicitly:
///
///   dotnet test --filter "Category=LiveFederation"
///
/// Requires outbound internet egress to mastodon.world.
/// </summary>
public class LiveFederationTests
{
    [Fact]
    [Trait("Category", "LiveFederation")]
    public async Task ResolveActor_RealMastodonWorld_ReturnsActor()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var service = new WebFingerService(httpClient, NullLogger<WebFingerService>.Instance);

        var actor = await service.ResolveActorAsync("RayvenMX@mastodon.world");

        Assert.NotNull(actor);
        Assert.Equal("RayvenMX", actor!.PreferredUsername);
        Assert.NotNull(actor.Id);
        Assert.Contains("mastodon.world", actor.Id);
        // The actor's endpoints (inbox / sharedInbox) are what outbound
        // delivery must target — assert at least one is present.
        var hasInbox = !string.IsNullOrEmpty(actor.Inbox)
                      || !string.IsNullOrEmpty(actor.Endpoints?.SharedInbox);
        Assert.True(hasInbox, "Resolved actor should expose an inbox or endpoints.sharedInbox");
    }

    [Fact]
    [Trait("Category", "LiveFederation")]
    public async Task ResolveActor_RealMastodonWorld_HandlesUnknownGracefully()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var service = new WebFingerService(httpClient, NullLogger<WebFingerService>.Instance);

        // A handle that does not resolve should return null (not throw).
        var actor = await service.ResolveActorAsync("this-user-definitely-does-not-exist-xyz@mastodon.world");
        Assert.Null(actor);
    }

    [Fact]
    [Trait("Category", "LiveFederation")]
    public async Task Receive_RealMastodonWorldNote_ProcessesAndStores()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var webFinger = new WebFingerService(httpClient, NullLogger<WebFingerService>.Instance);

        // 1. Resolve the real remote actor (verified path from the outbound tests).
        var actor = await webFinger.ResolveActorAsync("RayvenMX@mastodon.world");
        Assert.NotNull(actor);
        Assert.NotNull(actor!.Outbox);

        // 2. Walk the real outbox OrderedCollection -> first page -> first item.
        var collectionJson = await GetAsync(httpClient, actor.Outbox);
        using var collectionDoc = JsonDocument.Parse(collectionJson);
        var first = collectionDoc.RootElement.GetProperty("first").GetString();
        Assert.NotNull(first);

        var pageJson = await GetAsync(httpClient, first);
        using var pageDoc = JsonDocument.Parse(pageJson);
        var items = pageDoc.RootElement.GetProperty("orderedItems");
        Assert.True(items.GetArrayLength() > 0, "Real outbox page should contain at least one activity");
        var firstActivityJson = items[0].GetRawText();

        // 3. Deserialize the REAL activity through the app's Activity model.
        var activity = JsonSerializer.Deserialize<Activity>(
            firstActivityJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(activity);
        Assert.False(string.IsNullOrEmpty(activity!.Id));
        Assert.False(string.IsNullOrEmpty(activity.Type));
        Assert.False(string.IsNullOrEmpty(activity.ActorId));
        Assert.False(string.IsNullOrEmpty(activity.ObjectId));

        // 4. Run it through the REAL inbound pipeline (InboxProcessor) and prove
        //    it is stored — i.e. we can receive and store a real remote activity.
        var repository = new InMemoryActivityPubRepository();
        var processor = new InboxProcessor(repository, NullLogger<InboxProcessor>.Instance);
        await processor.HandleEventAsync(new ActivityReceivedEvent(activity));

        var stored = await repository.GetActivityAsync(activity.Id!);
        Assert.NotNull(stored);
        Assert.Equal(activity.Type, stored!.Type);
        Assert.Equal(activity.ObjectId, stored.ObjectId);
    }

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/activity+json"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
