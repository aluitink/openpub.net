using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Reject activity in Activity Streams 2.0
/// </summary>
public class Reject : Activity
{
    /// <summary>
    /// The object being rejected
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; set; }
}