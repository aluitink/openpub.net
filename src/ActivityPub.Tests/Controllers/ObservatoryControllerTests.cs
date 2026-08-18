using ActivityPub.Core.API.Controllers.Discovery;
using ActivityPub.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace ActivityPub.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="ObservatoryController"/> — the Mastodon
/// Observatory compliance endpoints (NodeInfo 2.0/2.1, the
/// <c>/.well-known/nodeinfo</c> discovery document, the host-meta XRD, and
/// <c>/x-nodeinfo2</c>), which previously had no direct unit test. Drives the
/// controller with a <see cref="DefaultHttpContext"/> and asserts on the
/// returned <see cref="ObjectResult"/> / XML.
/// </summary>
public class ObservatoryControllerTests
{
    private static ObservatoryController Build(string host = "example.com", string scheme = "https")
    {
        var controller = new ObservatoryController(
            Options.Create(new ActivityPubOptions { Domain = "fallback.example.com" }));
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    [Fact]
    public void NodeInfoDiscovery_ListsVersionsAndNodeLink()
    {
        var controller = Build();

        var ok = Assert.IsAssignableFrom<ObjectResult>(controller.GetNodeInfoDiscovery());

        using var el = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var root = el.RootElement;
        var versions = root.GetProperty("versions").EnumerateArray().Select(v => v.GetString()).ToHashSet();
        Assert.Contains("2.0", versions);
        Assert.Contains("2.1", versions);
        Assert.Equal("https://example.com/nodeinfo/2.1", root.GetProperty("node").GetString());
    }

    [Fact]
    public void NodeInfo20_ReturnsVersion20()
    {
        var controller = Build();

        var ok = Assert.IsAssignableFrom<ObjectResult>(controller.GetNodeInfo20());
        var value = Assert.IsType<ActivityPub.Core.Models.NodeInfoResponse>(ok.Value);

        Assert.Equal("2.0", value.Version);
    }

    [Fact]
    public void NodeInfo21_ReturnsVersion21()
    {
        var controller = Build();

        var ok = Assert.IsAssignableFrom<ObjectResult>(controller.GetNodeInfo21());
        var value = Assert.IsType<ActivityPub.Core.Models.NodeInfoResponse>(ok.Value);

        Assert.Equal("2.1", value.Version);
    }

    [Fact]
    public void NodeInfo_IncludesSoftwareProtocolsServices()
    {
        var controller = Build();

        var ok = Assert.IsAssignableFrom<ObjectResult>(controller.GetNodeInfo21());
        var value = Assert.IsType<ActivityPub.Core.Models.NodeInfoResponse>(ok.Value);

        Assert.Equal("ActivityPub.NET", value.Software.Name);
        Assert.Equal("0.1.0", value.Software.Version);
        Assert.Equal(2, value.Protocols.Length);
        // inbox is subscribed, outbox is not.
        Assert.True(value.Services.Inbox[0].Subscribed);
        Assert.False(value.Services.Outbox[0].Subscribed);
        Assert.True(value.OpenRegistrations);
    }

    [Fact]
    public void HostMeta_ReturnsXrdWithWebfingerTemplate()
    {
        var controller = Build();

        var content = Assert.IsType<ContentResult>(controller.GetHostMeta());

        Assert.Equal("application/xrd+xml", content.ContentType);
        Assert.Contains("<XRD", content.Content);
        Assert.Contains("application/xrd+xml", content.ContentType);
        Assert.Contains("/.well-known/webfinger?resource={uri}", content.Content);
        Assert.Contains("https://example.com/.well-known/webfinger", content.Content);
    }

    [Fact]
    public void XNodeInfo2_ReturnsXrdPointingAtNodeInfo()
    {
        var controller = Build();

        var content = Assert.IsType<ContentResult>(controller.GetXNodeInfo2());

        Assert.Equal("application/xrd+xml", content.ContentType);
        Assert.Contains("<XRD", content.Content);
        Assert.Contains("http://nodeinfo.diasofa.org/NS/schema/2.0", content.Content);
        Assert.Contains("https://example.com/nodeinfo/2.1", content.Content);
    }

    [Fact]
    public void BaseUrl_UsesRequestSchemeAndHost()
    {
        // Regression guard: the Observatory endpoints must reflect the request
        // scheme + host (so a request arriving over http, e.g. before the
        // reverse proxy forwards the original proto, yields http:// URLs —
        // they must NOT be force-rewritten to https://).
        var controller = Build(scheme: "http");

        var content = Assert.IsType<ContentResult>(controller.GetHostMeta());

        Assert.Contains("http://example.com/.well-known/webfinger", content.Content);
        Assert.DoesNotContain("https://", content.Content);
    }
}
