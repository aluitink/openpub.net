using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Object in Activity Streams 2.0
/// </summary>
public class Object
{
    /// <summary>
    /// The @context for JSON-LD
    /// </summary>
    [JsonPropertyName("@context")]
    public string? Context { get; set; }

    /// <summary>
    /// The unique identifier for the object
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// The type of object (e.g., "Note", "Article", "Event")
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// The name or title of the object
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The content of the object
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// The media type of the content (e.g., "text/html", "text/markdown")
    /// </summary>
    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    /// <summary>
    /// The URL to the object
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// The author of the object
    /// </summary>
    [JsonPropertyName("attributedTo")]
    public string? AttributedTo { get; set; }

    /// <summary>
    /// The published date of the object
    /// </summary>
    [JsonPropertyName("published")]
    public DateTime? Published { get; set; }

    /// <summary>
    /// The updated date of the object
    /// </summary>
    [JsonPropertyName("updated")]
    public DateTime? Updated { get; set; }

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
    public OrderedCollection? Replies { get; set; }

    /// <summary>
    /// The tag list
    /// </summary>
    [JsonPropertyName("tag")]
    public ICollection<string>? Tag { get; set; }

    /// <summary>
    /// The attachment list
    /// </summary>
    [JsonPropertyName("attachment")]
    public ICollection<string>? Attachment { get; set; }

    /// <summary>
    /// The to list
    /// </summary>
    [JsonPropertyName("to")]
    public ICollection<string>? To { get; set; }

    /// <summary>
    /// The cc list
    /// </summary>
    [JsonPropertyName("cc")]
    public ICollection<string>? Cc { get; set; }

    /// <summary>
    /// The bcc list
    /// </summary>
    [JsonPropertyName("bcc")]
    public ICollection<string>? Bcc { get; set; }

    /// <summary>
    /// The audience
    /// </summary>
    [JsonPropertyName("audience")]
    public string? Audience { get; set; }

    /// <summary>
    /// Additional properties that are not explicitly defined
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, object?>? AdditionalProperties { get; set; }
}