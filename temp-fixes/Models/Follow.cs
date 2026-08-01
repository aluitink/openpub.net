using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Follow activity in Activity Streams 2.0
/// </summary>
public class Follow : Activity
{
    /// <summary>
    /// The actor being followed
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; set; }
}