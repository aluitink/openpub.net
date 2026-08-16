namespace ActivityPub.WebUI.Services;

public interface IAuditLogService
{
    Task LogActionAsync(string adminUsername, string action, string targetId, string? details);
    Task<ICollection<AuditLogEntry>> GetRecentEntriesAsync(int limit = 50);
}

public class AuditLogService : IAuditLogService
{
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
        await Task.CompletedTask;
    }

    public async Task<ICollection<AuditLogEntry>> GetRecentEntriesAsync(int limit = 50)
    {
        var entries = _entries.ToArray().Take(limit).ToList();
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
