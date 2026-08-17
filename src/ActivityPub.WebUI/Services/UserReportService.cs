namespace ActivityPub.WebUI.Services;

public interface IUserReportService
{
    Task SubmitReportAsync(string reporterUsername, string targetUsername, string reason, string? activityId);
    Task<ICollection<UserReport>> GetPendingReportsAsync();
    Task<UserReport?> GetReportAsync(int id);
    Task DismissReportAsync(int id, string adminUsername, string? note);
    Task DeleteReportTargetAsync(int id, string adminUsername);
}

/// <summary>
/// In-memory user-report store.
///
/// Reports are kept in a bounded dictionary keyed by id. When the store exceeds
/// <see cref="MaxReports"/>, the oldest resolved (non-pending) reports are evicted
/// so the structure cannot grow without limit over the process lifetime (the
/// previous implementation queued every report and never removed any). Pending
/// reports are never evicted while a bound would force it, so in-flight
/// moderation is not lost.
/// </summary>
public class UserReportService : IUserReportService
{
    /// <summary>Maximum number of reports retained in memory.</summary>
    public const int MaxReports = 10_000;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, UserReport> _reports = new();
    private static int _nextId = 1;

    public async Task SubmitReportAsync(string reporterUsername, string targetUsername, string reason, string? activityId)
    {
        var id = Interlocked.Increment(ref _nextId);
        _reports[id] = new UserReport
        {
            Id = id,
            ReporterUsername = reporterUsername,
            TargetUsername = targetUsername,
            Reason = reason,
            ActivityId = activityId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        TrimResolved();
        await Task.CompletedTask;
    }

    public async Task<ICollection<UserReport>> GetPendingReportsAsync()
    {
        var result = _reports.Values.Where(r => r.Status == "pending")
            .OrderBy(r => r.Id)
            .ToList();
        await Task.CompletedTask;
        return result;
    }

    public async Task<UserReport?> GetReportAsync(int id)
    {
        _reports.TryGetValue(id, out var result);
        await Task.CompletedTask;
        return result;
    }

    public async Task DismissReportAsync(int id, string adminUsername, string? note)
    {
        if (_reports.TryGetValue(id, out var report))
        {
            report.Status = "dismissed";
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolvedBy = adminUsername;
            report.ResolverNote = note;
        }
        await Task.CompletedTask;
    }

    public async Task DeleteReportTargetAsync(int id, string adminUsername)
    {
        if (_reports.TryGetValue(id, out var report))
        {
            report.Status = "action_taken";
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolvedBy = adminUsername;
            report.ResolverNote = "Target content deleted";
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Evicts the oldest resolved (non-pending) reports once the store exceeds
    /// <see cref="MaxReports"/>, keeping pending reports intact. This bounds the
    /// store's growth while preserving anything an admin still needs to act on.
    /// </summary>
    private static void TrimResolved()
    {
        if (_reports.Count <= MaxReports)
            return;

        // Evict resolved reports oldest-first until back under the bound.
        var resolved = _reports.Values
            .Where(r => r.Status != "pending")
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .ToList();

        foreach (var id in resolved)
        {
            if (_reports.Count <= MaxReports)
                break;
            _reports.TryRemove(id, out _);
        }
    }
}

public class UserReport
{
    public int Id { get; set; }
    public string ReporterUsername { get; set; } = "";
    public string TargetUsername { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? ActivityId { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolverNote { get; set; }
}
