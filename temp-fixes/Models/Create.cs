using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Create activity in Activity Streams 2.0
/// </summary>
public class Create : Activity
{
    /// <summary>
    /// The object being created
    /// </summary>
    [JsonPropertyName("object")]
    public required Object Object { get; set; }
}