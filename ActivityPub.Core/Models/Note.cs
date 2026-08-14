using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Note in Activity Streams 2.0
/// </summary>
public class Note : Object
{
    /// <summary>
    /// The in-reply-to reference
    /// </summary>
    [JsonPropertyName("inReplyTo")]
    public new string? InReplyTo { get; set; }
    
    /// <summary>
    /// The parent object
    /// </summary>
    [JsonPropertyName("parent")]
    public new string? Parent { get; set; }
    
    /// <summary>
    /// The replies collection
    /// </summary>
    [JsonPropertyName("replies")]
    public new OrderedCollection? Replies { get; set; }
    
    /// <summary>
    /// Additional properties that are not explicitly defined
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, object?>? AdditionalProperties { get; set; }
}