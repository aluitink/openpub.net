using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Event object in Activity Streams 2.0
/// </summary>
public class Event : Object
{
    /// <summary>
    /// The event's start time
    /// </summary>
    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }
    
    /// <summary>
    /// The event's end time
    /// </summary>
    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// The event's location
    /// </summary>
    [JsonPropertyName("location")]
    public string? Location { get; set; }
    
    /// <summary>
    /// The event's attendees
    /// </summary>
    [JsonPropertyName("attendees")]
    public ICollection<string> Attendees { get; set; } = new List<string>();
}