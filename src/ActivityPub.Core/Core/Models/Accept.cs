using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Accept activity in Activity Streams 2.0
/// Used to accept Follow, Like, or other activities
/// </summary>
public class Accept : Activity
{
    /// <summary>
    /// The original object being accepted (e.g., a Follow activity)
    /// </summary>
    [JsonPropertyName("origin")]
    public object? Origin { get; set; }

    /// <summary>
    /// The result or target of the accept activity
    /// </summary>
    [JsonPropertyName("result")]
    public object? Result { get; set; }
}
