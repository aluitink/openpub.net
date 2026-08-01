using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Represents a public key used for verifying signatures
/// </summary>
public record PublicKey
{
    /// <summary>
    /// The unique identifier for the public key
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    /// <summary>
    /// The owner of the public key
    /// </summary>
    [JsonPropertyName("owner")]
    public required string Owner { get; set; }
    
    /// <summary>
    /// The public key in PEM format
    /// </summary>
    [JsonPropertyName("publicKeyPem")]
    public required string PublicKeyPem { get; set; }
}