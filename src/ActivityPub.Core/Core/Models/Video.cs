using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Video object in Activity Streams 2.0
/// </summary>
public class Video : Object
{
    /// <summary>
    /// The video's duration
    /// </summary>
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
    
    /// <summary>
    /// The video's width
    /// </summary>
    [JsonPropertyName("width")]
    public int? Width { get; set; }
    
    /// <summary>
    /// The video's height
    /// </summary>
    [JsonPropertyName("height")]
    public int? Height { get; set; }
}