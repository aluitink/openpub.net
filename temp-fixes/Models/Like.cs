using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Like activity in Activity Streams 2.0
/// </summary>
public class Like : Activity
{
    /// <summary>
    /// The object being liked
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; set; }
}