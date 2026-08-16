using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Article in Activity Streams 2.0 (long-form content)
/// </summary>
public class Article : Object
{
    /// <summary>
    /// The name or title of the article
    /// </summary>
    [JsonPropertyName("name")]
    public new string? Name { get; set; }

    /// <summary>
    /// A short summary or description of the article
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// The full content of the article (HTML)
    /// </summary>
    [JsonPropertyName("content")]
    public new string? Content { get; set; }

    /// <summary>
    /// The media type of the content
    /// </summary>
    [JsonPropertyName("mediaType")]
    public new string? MediaType { get; set; }

    /// <summary>
    /// The URL to the article
    /// </summary>
    [JsonPropertyName("url")]
    public new string? Url { get; set; }

    /// <summary>
    /// The updated date of the article
    /// </summary>
    [JsonPropertyName("updated")]
    public new DateTime? Updated { get; set; }

    /// <summary>
    /// The in-reply-to reference
    /// </summary>
    [JsonPropertyName("inReplyTo")]
    public string? InReplyTo { get; set; }

    /// <summary>
    /// The parent object
    /// </summary>
    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

    /// <summary>
    /// The replies collection
    /// </summary>
    [JsonPropertyName("replies")]
    public new OrderedCollection? Replies { get; set; }
}
