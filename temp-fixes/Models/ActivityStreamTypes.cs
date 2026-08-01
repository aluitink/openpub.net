using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents the base Activity Streams 2.0 vocabulary types
/// </summary>
public record ActivityStreamTypes
{
    /// <summary>
    /// The primary type for all Activity Streams objects
    /// </summary>
    public const string ObjectType = "Object";
    
    /// <summary>
    /// The primary type for all Activity Streams activities
    /// </summary>
    public const string ActivityType = "Activity";
    
    /// <summary>
    /// The primary type for all Activity Streams actors
    /// </summary>
    public const string ActorType = "Actor";
}