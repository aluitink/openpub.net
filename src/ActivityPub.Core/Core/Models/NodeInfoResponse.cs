using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Software information for the NodeInfo response
/// </summary>
public class NodeInfoSoftware
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "ActivityPub.NET";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.1.0";
}

/// <summary>
/// Protocol support information
/// </summary>
public class NodeInfoProtocol
{
    [JsonPropertyName("subscribed")]
    public bool Subscribed { get; set; }

    [JsonPropertyName("versions")]
    public string[] Versions { get; set; } = Array.Empty<string>();
}

/// <summary>
/// NodeInfo 2.0/2.1 response body
/// </summary>
public class NodeInfoResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "2.1";

    [JsonPropertyName("software")]
    public NodeInfoSoftware Software { get; set; } = new();

    [JsonPropertyName("protocols")]
    public NodeInfoProtocol[] Protocols { get; set; } = Array.Empty<NodeInfoProtocol>();

    [JsonPropertyName("services")]
    public NodeInfoServices Services { get; set; } = new();

    [JsonPropertyName("openRegistrations")]
    public bool OpenRegistrations { get; set; }

    [JsonPropertyName("usage")]
    public NodeInfoUsage Usage { get; set; } = new();

    [JsonPropertyName("openCollective")]
    public string? OpenCollective { get; set; }
}

/// <summary>
/// Service information within NodeInfo
/// </summary>
public class NodeInfoServices
{
    [JsonPropertyName("inbox")]
    public NodeInfoProtocol[] Inbox { get; set; } = Array.Empty<NodeInfoProtocol>();

    [JsonPropertyName("outbox")]
    public NodeInfoProtocol[] Outbox { get; set; } = Array.Empty<NodeInfoProtocol>();
}

/// <summary>
/// Usage statistics within NodeInfo
/// </summary>
public class NodeInfoUsage
{
    [JsonPropertyName("users")]
    public NodeInfoUsers Users { get; set; } = new();

    [JsonPropertyName("localPosts")]
    public int LocalPosts { get; set; }

    [JsonPropertyName("localComments")]
    public int LocalComments { get; set; }
}

/// <summary>
/// User statistics within NodeInfo usage
/// </summary>
public class NodeInfoUsers
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("activeHalfyear")]
    public int ActiveHalfyear { get; set; }

    [JsonPropertyName("activeMonth")]
    public int ActiveMonth { get; set; }
}
