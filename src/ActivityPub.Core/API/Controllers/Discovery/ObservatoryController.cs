using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using Microsoft.Extensions.Options;

namespace ActivityPub.Core.API.Controllers.Discovery;

/// <summary>
/// Mastodon Observatory compliance endpoints
/// https://github.com/mastodon/mastodon/blob/main/docs/observatory.md
/// </summary>
[ApiController]
public class ObservatoryController : ControllerBase
{
    private readonly ActivityPubOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public ObservatoryController(IOptions<ActivityPubOptions> options)
    {
        _options = options.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <summary>
    /// NodeInfo 2.0 discovery endpoint
    /// GET /.well-known/nodeinfo
    /// </summary>
    [HttpGet(".well-known/nodeinfo")]
    public IActionResult GetNodeInfoDiscovery()
    {
        var response = new NodeInfoDiscoverResponse
        {
            Versions = new[] { "2.0", "2.1" },
            Node = $"{GetBaseUrl()}/nodeinfo/2.1",
        };

        return Ok(response);
    }

    /// <summary>
    /// NodeInfo 2.0 endpoint
    /// GET /nodeinfo/2.0
    /// </summary>
    [HttpGet("nodeinfo/2.0")]
    public IActionResult GetNodeInfo20()
    {
        var response = BuildNodeInfoResponse("2.0");
        return Ok(response);
    }

    /// <summary>
    /// NodeInfo 2.1 endpoint
    /// GET /nodeinfo/2.1
    /// </summary>
    [HttpGet("nodeinfo/2.1")]
    public IActionResult GetNodeInfo21()
    {
        var response = BuildNodeInfoResponse("2.1");
        return Ok(response);
    }

    /// <summary>
    /// Host-meta endpoint for XRD discovery
    /// GET /.well-known/host-meta
    /// </summary>
    [HttpGet(".well-known/host-meta")]
    public IActionResult GetHostMeta()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<XRD xmlns=\"http://docs.oasis-open.org/ns/xri/xrd-1.0\">");
        sb.AppendLine($"  <Link rel=\"lrdd\" template=\"{GetBaseUrl()}/.well-known/webfinger?resource={{uri}}\"/>");
        sb.AppendLine("</XRD>");

        return Content(sb.ToString(), "application/xrd+xml");
    }

    /// <summary>
    /// NodeInfo 2.0 discovery via host-meta
    /// GET /.well-known/x-nodeinfo2
    /// </summary>
    [HttpGet(".well-known/x-nodeinfo2")]
    public IActionResult GetXNodeInfo2()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<XRD xmlns=\"http://docs.oasis-open.org/ns/xri/xrd-1.0\">");
        sb.AppendLine($"  <Link rel=\"http://nodeinfo.diasofa.org/NS/schema/2.0\" href=\"{GetBaseUrl()}/nodeinfo/2.1\"/>");
        sb.AppendLine("</XRD>");

        return Content(sb.ToString(), "application/xrd+xml");
    }

    private NodeInfoResponse BuildNodeInfoResponse(string version)
    {
        return new NodeInfoResponse
        {
            Version = version,
            Software = new NodeInfoSoftware
            {
                Name = "ActivityPub.NET",
                Version = "0.1.0",
            },
            Protocols = new[]
            {
                new NodeInfoProtocol
                {
                    Subscribed = true,
                    Versions = new[] { "1.0" },
                },
                new NodeInfoProtocol
                {
                    Subscribed = false,
                    Versions = new[] { "1.0" },
                },
            },
            Services = new NodeInfoServices
            {
                Inbox = new[]
                {
                    new NodeInfoProtocol
                    {
                        Subscribed = true,
                        Versions = new[] { "1.0" },
                    },
                },
                Outbox = new[]
                {
                    new NodeInfoProtocol
                    {
                        Subscribed = false,
                        Versions = new[] { "1.0" },
                    },
                },
            },
            OpenRegistrations = true,
            Usage = new NodeInfoUsage
            {
                Users = new NodeInfoUsers
                {
                    Total = 0,
                    ActiveHalfyear = 0,
                    ActiveMonth = 0,
                },
                LocalPosts = 0,
                LocalComments = 0,
            },
        };
    }

    private string GetBaseUrl()
    {
        var scheme = Request.Scheme;
        var host = Request.Host.Value;
        return $"{scheme}://{host}";
    }
}
