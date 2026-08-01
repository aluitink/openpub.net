using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Note object in Activity Streams 2.0
/// </summary>
public class Note : Object
{
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
}