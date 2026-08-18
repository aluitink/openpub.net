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
}
