using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Article in Activity Streams 2.0
/// </summary>
public class Article : Object
{
    /// <summary>
    /// The content of the article
    /// </summary>
    [JsonPropertyName("content")]
    public new required string Content { get; set; }

    /// <summary>
    /// The media type of the content
    /// </summary>
    [JsonPropertyName("mediaType")]
    public new required string MediaType { get; set; }
}