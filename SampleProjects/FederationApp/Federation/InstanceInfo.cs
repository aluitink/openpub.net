using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;

namespace FederationApp.Federation;

public class InstanceInfo
{
    public string InstanceId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string InboxUrl { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public DateTime LastContacted { get; set; }
    public bool IsConnected { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
}
