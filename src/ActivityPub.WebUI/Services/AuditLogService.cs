namespace ActivityPub.WebUI.Services;

public interface IAuditLogService
{
    Task LogActionAsync(string adminUsername, string action, string targetId, string? details);
    Task<ICollection<AuditLogEntry>> GetRecentEntriesAsync(int limit = 50);
}

/// <summary>
/// In-memory audit log of administrative actions.
///
/// The log is a bounded ring: it retains at most <see cref="MaxEntries"/> of the
/// most recent entries and drops the oldest beyond that. Without the bound, the
/// static queue would accumulate every admin action for the process lifetime and
/// grow without limit (a memory leak in a long-running server).
/// </summary>
public class AuditLogService : IAuditLogService
{
    /// <summary>Maximum number of audit entries retained in memory.</summary>
    public const int MaxEntries = 10_000;

    private static readonly System.Collections.Concurrent.ConcurrentQueue<AuditLogEntry> _entries = new();

    public async Task LogActionAsync(string adminUsername, string action, string targetId, string? details)
    {
        _entries.Enqueue(new AuditLogEntry
        {
            AdminUsername = adminUsername,
            Action = action,
            TargetId = targetId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });

        // Trim the oldest entries once the ring exceeds its bound so the queue
        // stays bounded regardless of how long the process has been running.
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
        }

        await Task.CompletedTask;
    }

    public async Task<ICollection<AuditLogEntry>> GetRecentEntriesAsync(int limit = 50)
    {
        // The queue is oldest-first; the most recent entries are at the tail, so
        // take the last <paramref name="limit"/> entries.
        var all = _entries.ToArray();
        var start = Math.Max(0, all.Length - limit);
        var entries = all[start..].Reverse().ToList();
        await Task.CompletedTask;
        return entries;
    }
}

public class AuditLogEntry
{
    public string AdminUsername { get; set; } = "";
    public string Action { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}
