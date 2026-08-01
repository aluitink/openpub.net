using System.Text\Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Undo activity in Activity Streams 2.0
/// </summary>
public class Undo : Activity
{
    /// <summary>
    /// The activity being undone
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; set; }
}