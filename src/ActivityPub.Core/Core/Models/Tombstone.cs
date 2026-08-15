using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Tombstone object in Activity Streams 2.0
/// </summary>
public record Tombstone
{
    /// <summary>
    /// The @context for JSON-LD
    /// </summary>
    [JsonPropertyName("@context")]
    public string? Context { get; set; }

    /// <summary>
    /// The unique identifier for the tombstone
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// The type of object (always "Tombstone")
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// The deletion date
    /// </summary>
    [JsonPropertyName("deleted")]
    public DateTime? Deleted { get; set; }

    /// <summary>
    /// The reason for deletion
    /// </summary>
    [JsonPropertyName("formerType")]
    public string? FormerType { get; set; }
}