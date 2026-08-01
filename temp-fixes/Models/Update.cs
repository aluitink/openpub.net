using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Update activity in Activity Streams 2.0
/// </summary>
public class Update : Activity
{
    /// <summary>
    /// The object being updated
    /// </summary>
    [JsonPropertyName("object")]
    public required Object Object { get; set; }
}