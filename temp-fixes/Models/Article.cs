using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Article object in Activity Streams 2.0
/// </summary>
public class Article : Object
{
    /// <summary>
    /// The article's content
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    
    /// <summary>
    /// The article's media type
    /// </summary>
    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }
}