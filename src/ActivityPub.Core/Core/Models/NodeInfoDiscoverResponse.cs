using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// NodeInfo 2.0 discovery response (RFC 2391)
/// </summary>
public class NodeInfoDiscoverResponse
{
    /// <summary>
    /// Versions of the NodeInfo spec supported by this server
    /// </summary>
    [JsonPropertyName("versions")]
    public string[] Versions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The link to the NodeInfo 2.0 endpoint
    /// </summary>
    [JsonPropertyName("node")]
    public string Node { get; set; } = string.Empty;
}
