namespace ActivityPub.Core.Options;

/// <summary>
/// Configuration options for ActivityPub
/// </summary>
public class ActivityPubOptions
{
    /// <summary>
    /// The domain/host for the ActivityPub server
    /// </summary>
    public string Domain { get; set; } = "localhost";
    
    /// <summary>
    /// Path prefix for user/actor endpoints
    /// </summary>
    public string UserPath { get; set; } = "/users";
    
    /// <summary>
    /// Path for inbox endpoint
    /// </summary>
    public string InboxPath { get; set; } = "/inbox";
    
    /// <summary>
    /// Path for outbox endpoint
    /// </summary>
    public string OutboxPath { get; set; } = "/outbox";
    
    /// <summary>
    /// Path for followers endpoint
    /// </summary>
    public string FollowersPath { get; set; } = "/followers";
    
    /// <summary>
    /// Path for following endpoint
    /// </summary>
    public string FollowingPath { get; set; } = "/following";
    
    /// <summary>
    /// Path for shared inbox endpoint
    /// </summary>
    public string SharedInboxPath { get; set; } = "/inbox";
    
    /// <summary>
    /// Maximum activity queue size
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;
    
    /// <summary>
    /// Enable HTTP signature verification
    /// </summary>
    public bool EnableSignatureVerification { get; set; } = true;
    
    /// <summary>
    /// Enable ActivityPub federation
    /// </summary>
    public bool EnableFederation { get; set; } = false;
}
