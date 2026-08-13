using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Ordered Collection in Activity Streams 2.0
/// </summary>
public class OrderedCollection
{
    /// <summary>
    /// The unique identifier for the collection
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    /// <summary>
    /// The type of collection (typically "OrderedCollection")
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    /// <summary>
    /// The name of the collection
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    /// <summary>
    /// The ordered items in the collection
    /// </summary>
    [JsonPropertyName("orderedItems")]
    public ICollection<string> OrderedItems { get; set; } = new List<string>();
    
    /// <summary>
    /// The total number of items in the collection
    /// </summary>
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }
    
    /// <summary>
    /// The first item in the collection
    /// </summary>
    [JsonPropertyName("first")]
    public string? First { get; set; }
    
    /// <summary>
    /// The last item in the collection
    /// </summary>
    [JsonPropertyName("last")]
    public string? Last { get; set; }
    
    /// <summary>
    /// The summary of the collection
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}