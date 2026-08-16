using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Community/Group in Activity Streams 2.0 (as a CollectionPage-like actor group)
/// </summary>
public class Community
{
    [JsonPropertyName("@context")]
    public string? Context { get; set; }

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("icon")]
    public object? Icon { get; set; }

    [JsonPropertyName("image")]
    public object? Image { get; set; }

    [JsonPropertyName("inbox")]
    public string? Inbox { get; set; }

    [JsonPropertyName("outbox")]
    public string? Outbox { get; set; }

    [JsonPropertyName("followers")]
    public string? Followers { get; set; }

    [JsonPropertyName("following")]
    public string? Following { get; set; }

    [JsonPropertyName("published")]
    public DateTime? Published { get; set; }

    [JsonPropertyName("manuallyApprovesFollowers")]
    public bool ManuallyApprovesFollowers { get; set; }

    [JsonPropertyName("attributedTo")]
    public string? OwnerId { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
