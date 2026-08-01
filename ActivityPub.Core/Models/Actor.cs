using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents an Actor in Activity Streams 2.0
/// </summary>
public record Actor
{
    /// <summary>
    /// The @context for JSON-LD
    /// </summary>
    [JsonPropertyName("@context")]
    public string? Context { get; set; }
    
    /// <summary>
    /// The unique identifier for the actor
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    /// <summary>
    /// The type of object (typically "Person", "Organization", or "Service")
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    
    /// <summary>
    /// The display name of the actor
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    /// <summary>
    /// The preferred username of the actor
    /// </summary>
    [JsonPropertyName("preferredUsername")]
    public string? PreferredUsername { get; set; }
    
    /// <summary>
    /// The URL to the actor's profile page
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    
    /// <summary>
    /// The actor's public key
    /// </summary>
    [JsonPropertyName("publicKey")]
    public PublicKey? PublicKey { get; set; }
    
    /// <summary>
    /// The actor's inbox URL
    /// </summary>
    [JsonPropertyName("inbox")]
    public string? Inbox { get; set; }
    
    /// <summary>
    /// The actor's outbox URL
    /// </summary>
    [JsonPropertyName("outbox")]
    public string? Outbox { get; set; }
    
    /// <summary>
    /// The actor's followers collection
    /// </summary>
    [JsonPropertyName("followers")]
    public string? Followers { get; set; }
    
    /// <summary>
    /// The actor's following collection
    /// </summary>
    [JsonPropertyName("following")]
    public string? Following { get; set; }
    
    /// <summary>
    /// The actor's liked items collection
    /// </summary>
    [JsonPropertyName("liked")]
    public string? Liked { get; set; }
    
    /// <summary>
    /// The actor's profile image
    /// </summary>
    [JsonPropertyName("icon")]
    public Image? Icon { get; set; }
    
    /// <summary>
    /// The actor's banner image
    /// </summary>
    [JsonPropertyName("image")]
    public Image? Image { get; set; }
    
    /// <summary>
    /// The actor's summary
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    /// <summary>
    /// The actor's published date
    /// </summary>
    [JsonPropertyName("published")]
    public DateTime? Published { get; set; }
    
    /// <summary>
    /// The actor's updated date
    /// </summary>
    [JsonPropertyName("updated")]
    public DateTime? Updated { get; set; }
    
    /// <summary>
    /// The actor's local domain
    /// </summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; set; }
    
    /// <summary>
    /// Whether the actor manually approves followers
    /// </summary>
    [JsonPropertyName("manuallyApprovesFollowers")]
    public bool ManuallyApprovesFollowers { get; set; }
    
    /// <summary>
    /// The actor's endpoints
    /// </summary>
    [JsonPropertyName("endpoints")]
    public Endpoints? Endpoints { get; set; }
    
    /// <summary>
    /// The actor's hashtag
    /// </summary>
    [JsonPropertyName("hashtag")]
    public bool Hashtag { get; set; }
    
    /// <summary>
    /// The actor's shared inbox
    /// </summary>
    [JsonPropertyName("sharedInbox")]
    public string? SharedInbox { get; set; }
    
    /// <summary>
    /// Additional properties that may not be covered by the standard schema
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, object>? AdditionalProperties { get; set; }
}