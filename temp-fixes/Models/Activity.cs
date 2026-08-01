using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Activity in Activity Streams 2.0
/// </summary>
public record Activity
{
    /// <summary>
    /// The @context for JSON-LD
    /// </summary>
    [JsonPropertyName("@context")]
    public string? Context { get; set; }
    
    /// <summary>
    /// The unique identifier for the activity
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    /// <summary>
    /// The type of activity (e.g., "Create", "Follow", "Like")
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    
    /// <summary>
    /// The actor performing the activity
    /// </summary>
    [JsonPropertyName("actor")]
    public required string Actor { get; set; }
    
    /// <summary>
    /// The object being acted upon
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; set; }
    
    /// <summary>
    /// The target of the activity (optional)
    /// </summary>
    [JsonPropertyName("target")]
    public string? Target { get; set; }
    
    /// <summary>
    /// The summary of the activity
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    /// <summary>
    /// The published date of the activity
    /// </summary>
    [JsonPropertyName("published")]
    public DateTime? Published { get; set; }
    
    /// <summary>
    /// The updated date of the activity
    /// </summary>
    [JsonPropertyName("updated")]
    public DateTime? Updated { get; set; }
    
    /// <summary>
    /// The instrument used to perform the activity (optional)
    /// </summary>
    [JsonPropertyName("instrument")]
    public string? Instrument { get; set; }
    
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
    /// The bto list
    /// </summary>
    [JsonPropertyName("bto")]
    public ICollection<string>? Bto { get; set; }
    
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
    /// The attributedTo property for attaching activities to actors
    /// </summary>
    [JsonPropertyName("attributedTo")]
    public string? AttributedTo { get; set; }
    
    /// <summary>
    /// Additional properties that may not be covered by the standard schema
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, object>? AdditionalProperties { get; set; }
}