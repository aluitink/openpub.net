using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace ActivityPub.Core.Services;

public class FederationHealthService : IFederationHealthService
{
    private readonly IActivityPubRepository _repository;
    private readonly ActivityPubDbContext _context;
    private readonly ILogger<FederationHealthService> _logger;
    private readonly HttpClient _httpClient;
    private static readonly List<FederationErrorLog> _errorLog = new();
    private static readonly object _lock = new();

    public FederationHealthService(
        IActivityPubRepository repository,
        ActivityPubDbContext context,
        ILogger<FederationHealthService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _repository = repository;
        _context = context;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("FederationHealth");
    }

    public async Task<FederationHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new FederationHealthStatus
        {
            LastChecked = DateTime.UtcNow,
            DeliveryQueue = await GetDeliveryQueueStatsAsync(cancellationToken),
            ActivityProcessing = await GetActivityProcessingStatsAsync(cancellationToken),
            Database = await GetDatabaseStatsAsync(cancellationToken)
        };

        status.OverallStatus = DetermineOverallStatus(status);
        return status;
    }

    public async Task<DeliveryQueueStats> GetDeliveryQueueStatsAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _repository.GetPendingSharedInboxDeliveriesAsync(int.MaxValue);

        var stats = new DeliveryQueueStats
        {
            PendingCount = pending.Count(d => d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Processing),
            FailedCount = pending.Count(d => d.Status == DeliveryStatus.Failed),
            MaxRetriesExceededCount = pending.Count(d => d.Status == DeliveryStatus.MaxRetriesExceeded)
        };

        var queued = pending.Where(d => d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Processing)
            .OrderBy(d => d.CreatedAt);
        stats.OldestPending = queued.FirstOrDefault()?.CreatedAt;

        var delivered = pending.Where(d => d.Status == DeliveryStatus.Delivered)
            .OrderByDescending(d => d.UpdatedAt);
        stats.LastSuccessfulDelivery = delivered.FirstOrDefault()?.UpdatedAt;

        var total = pending.Count;
        var failedTotal = stats.FailedCount + stats.MaxRetriesExceededCount;
        stats.ErrorRate = total > 0 ? (double)failedTotal / total * 100 : 0;

        return stats;
    }

    public async Task<ICollection<RemoteServerProbeResult>> ProbeRemoteServersAsync(
        ICollection<string> serverIds, CancellationToken cancellationToken = default)
    {
        var results = new List<RemoteServerProbeResult>();

        foreach (var serverId in serverIds)
        {
            var domain = ExtractDomain(serverId);
            var result = new RemoteServerProbeResult
            {
                ServerId = serverId,
                Domain = domain
            };

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var webfingerUrl = $"https://{domain}/.well-known/webfinger?resource=acct:test@{domain}";
                var response = await _httpClient.GetAsync(webfingerUrl, cts.Token);

                sw.Stop();
                result.ResponseTimeMs = (int)sw.ElapsedMilliseconds;
                result.Reachable = response.IsSuccessStatusCode;

                if (!result.Reachable)
                {
                    result.ErrorMessage = $"HTTP {(int)response.StatusCode}";
                }
            }
            catch (TaskCanceledException)
            {
                result.Reachable = false;
                result.ErrorMessage = "Timeout (5s)";
            }
            catch (Exception ex)
            {
                result.Reachable = false;
                result.ErrorMessage = ex.Message;
            }

            result.ProbedAt = DateTime.UtcNow;
            results.Add(result);
        }

        return results;
    }

    public async Task<ICollection<FederationErrorLog>> GetRecentErrorsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var pending = await _repository.GetPendingSharedInboxDeliveriesAsync(limit * 10);

        var errors = pending
            .Where(d => d.Status == DeliveryStatus.Failed || d.Status == DeliveryStatus.MaxRetriesExceeded)
            .OrderByDescending(d => d.LastDeliveryAttempt ?? d.CreatedAt)
            .Take(limit)
            .Select(d => new FederationErrorLog
            {
                Id = d.Id,
                ActivityId = d.ActivityId,
                TargetActorId = d.TargetActorId,
                FailureReason = d.FailureReason ?? "Unknown",
                RetryCount = d.RetryCount,
                LastAttempt = d.LastDeliveryAttempt ?? d.CreatedAt
            })
            .ToList();

        return errors;
    }

    public async Task ClearErrorLogAsync(CancellationToken cancellationToken = default)
    {
        using var lockObj = new System.Threading.Mutex(false, "ActivityPubErrorLog");
        lockObj.WaitOne();
        try
        {
            lock (_lock)
            {
                _errorLog.Clear();
            }
        }
        finally
        {
            lockObj.ReleaseMutex();
        }
    }

    private async Task<ActivityProcessingStats> GetActivityProcessingStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new ActivityProcessingStats();

        var allActivities = await _context.Activities.ToListAsync(cancellationToken);
        stats.TotalActivities = allActivities.Count;

        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var oneDayAgo = DateTime.UtcNow.AddDays(-1);

        stats.ActivitiesLastHour = allActivities.Count(a => a.CreatedAt >= oneHourAgo);
        stats.ActivitiesLastDay = allActivities.Count(a => a.CreatedAt >= oneDayAgo);

        foreach (var activity in allActivities)
        {
            var type = ExtractActivityType(activity.JsonData);
            if (type != null)
            {
                stats.ActivityTypes[type] = stats.ActivityTypes.GetValueOrDefault(type, 0) + 1;
            }
        }

        return stats;
    }

    private async Task<DatabaseStats> GetDatabaseStatsAsync(CancellationToken cancellationToken = default)
    {
        return new DatabaseStats
        {
            TotalActors = await _context.Actors.CountAsync(cancellationToken),
            TotalActivities = await _context.Activities.CountAsync(cancellationToken),
            TotalDeliveries = await _context.SharedInboxDeliveries.CountAsync(cancellationToken)
        };
    }

    private static string DetermineOverallStatus(FederationHealthStatus status)
    {
        if (status.DeliveryQueue.ErrorRate > 50) return "Critical";
        if (status.DeliveryQueue.ErrorRate > 20) return "Degraded";
        if (status.DeliveryQueue.PendingCount > 1000) return "Warning";
        return "Healthy";
    }

    private static string ExtractDomain(string serverId)
    {
        var uri = new Uri(serverId);
        return uri.Host;
    }

    private static string? ExtractActivityType(string jsonData)
    {
        const string prefix = "\"type\":\"";
        var start = jsonData.IndexOf(prefix, StringComparison.Ordinal);
        if (start == -1) return null;

        start += prefix.Length;
        var end = jsonData.IndexOf("\"", start, StringComparison.Ordinal);
        if (end == -1) return null;

        return jsonData.Substring(start, end - start);
    }
}
