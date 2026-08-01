using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Announce activity in Activity Streams 2.0
/// </summary>
public class Announce : Activity
{
    /// <summary>
    /// The object being announced
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; set; }
}