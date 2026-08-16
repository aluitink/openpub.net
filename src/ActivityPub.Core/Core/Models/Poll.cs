using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a Poll in ActivityPub
/// </summary>
public class Poll
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; } = "Question";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("options")]
    public ICollection<string>? Options { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("expires")]
    public bool Expires { get; set; } = true;

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }

    [JsonPropertyName("votesCount")]
    public int VotesCount { get; set; }

    [JsonPropertyName("votersCount")]
    public int? VotersCount { get; set; }

    [JsonPropertyName("votes")]
    public ICollection<int>? Votes { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
