using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;

namespace ActivityPub.Core.Services;

public interface IFederationHealthService
{
    Task<FederationHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default);
    Task<DeliveryQueueStats> GetDeliveryQueueStatsAsync(CancellationToken cancellationToken = default);
    Task<ICollection<RemoteServerProbeResult>> ProbeRemoteServersAsync(ICollection<string> serverIds, CancellationToken cancellationToken = default);
    Task<ICollection<FederationErrorLog>> GetRecentErrorsAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task ClearErrorLogAsync(CancellationToken cancellationToken = default);
}

public class FederationHealthStatus
{
    public string OverallStatus { get; set; } = "Healthy";
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
    public DeliveryQueueStats DeliveryQueue { get; set; } = new();
    public ActivityProcessingStats ActivityProcessing { get; set; } = new();
    public DatabaseStats Database { get; set; } = new();
    public ICollection<RemoteServerProbeResult> RemoteServers { get; set; } = new List<RemoteServerProbeResult>();
}

public class DeliveryQueueStats
{
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
    public int MaxRetriesExceededCount { get; set; }
    public DateTime? OldestPending { get; set; }
    public DateTime? LastSuccessfulDelivery { get; set; }
    public double ErrorRate { get; set; }
}

public class ActivityProcessingStats
{
    public int TotalActivities { get; set; }
    public int ActivitiesLastHour { get; set; }
    public int ActivitiesLastDay { get; set; }
    public Dictionary<string, int> ActivityTypes { get; set; } = new();
}

public class DatabaseStats
{
    public int TotalActors { get; set; }
    public int TotalActivities { get; set; }
    public int TotalDeliveries { get; set; }
    public long EstimatedSizeBytes { get; set; }
}

public class RemoteServerProbeResult
{
    public string ServerId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public bool Reachable { get; set; }
    public int ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProbedAt { get; set; } = DateTime.UtcNow;
}

public class FederationErrorLog
{
    public string Id { get; set; } = string.Empty;
    public string ActivityId { get; set; } = string.Empty;
    public string TargetActorId { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public DateTime LastAttempt { get; set; }
}
