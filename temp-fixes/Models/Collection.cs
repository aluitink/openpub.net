using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Collection in Activity Streams 2.0
/// </summary>
public record Collection
{
    /// <summary>
    /// The @context for JSON-LD
    /// </summary>
    [JsonPropertyName("@context")]
    public string? Context { get; set; }
    
    /// <summary>
    /// The unique identifier for the collection
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    /// <summary>
    /// The type of collection (typically "Collection")
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    
    /// <summary>
    /// The name of the collection
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    /// <summary>
    /// The items in the collection
    /// </summary>
    [JsonPropertyName("items")]
    public ICollection<string> Items { get; set; } = new List<string>();
    
    /// <summary>
    /// The total number of items in the collection
    /// </summary>
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }
    
    /// <summary>
    /// The summary of the collection
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}