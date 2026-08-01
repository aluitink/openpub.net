using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Delete activity in Activity Streams 2.0
/// </summary>
public class Delete : Activity
{
    /// <summary>
    /// The object being deleted
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; set; }
}