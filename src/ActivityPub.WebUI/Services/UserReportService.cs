namespace ActivityPub.WebUI.Services;

public interface IUserReportService
{
    Task SubmitReportAsync(string reporterUsername, string targetUsername, string reason, string? activityId);
    Task<ICollection<UserReport>> GetPendingReportsAsync();
    Task<UserReport?> GetReportAsync(int id);
    Task DismissReportAsync(int id, string adminUsername, string? note);
    Task DeleteReportTargetAsync(int id, string adminUsername);
}

public class UserReportService : IUserReportService
{
    private static readonly System.Collections.Concurrent.ConcurrentQueue<UserReport> _reports = new();
    private static int _nextId = 1;

    public async Task SubmitReportAsync(string reporterUsername, string targetUsername, string reason, string? activityId)
    {
        var id = Interlocked.Increment(ref _nextId);
        _reports.Enqueue(new UserReport
        {
            Id = id,
            ReporterUsername = reporterUsername,
            TargetUsername = targetUsername,
            Reason = reason,
            ActivityId = activityId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        });
        await Task.CompletedTask;
    }

    public async Task<ICollection<UserReport>> GetPendingReportsAsync()
    {
        var result = _reports.ToArray().Where(r => r.Status == "pending").ToList();
        await Task.CompletedTask;
        return result;
    }

    public async Task<UserReport?> GetReportAsync(int id)
    {
        var result = _reports.ToArray().FirstOrDefault(r => r.Id == id);
        await Task.CompletedTask;
        return result;
    }

    public async Task DismissReportAsync(int id, string adminUsername, string? note)
    {
        foreach (var report in _reports)
        {
            if (report.Id == id)
            {
                report.Status = "dismissed";
                report.ResolvedAt = DateTime.UtcNow;
                report.ResolvedBy = adminUsername;
                report.ResolverNote = note;
                break;
            }
        }
        await Task.CompletedTask;
    }

    public async Task DeleteReportTargetAsync(int id, string adminUsername)
    {
        foreach (var report in _reports)
        {
            if (report.Id == id)
            {
                report.Status = "action_taken";
                report.ResolvedAt = DateTime.UtcNow;
                report.ResolvedBy = adminUsername;
                report.ResolverNote = "Target content deleted";
                break;
            }
        }
        await Task.CompletedTask;
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
