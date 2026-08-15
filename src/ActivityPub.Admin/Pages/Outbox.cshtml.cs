using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ActivityPub.Admin.Pages;

public class OutboxModel : PageModel
{
    public ICollection<OutboxEntry> OutboxActivities { get; private set; } = new List<OutboxEntry>();

    public void OnGet()
    {
        OutboxActivities = new List<OutboxEntry>();
    }
}

public class OutboxEntry
{
    public string ActivityId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TargetInbox { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime? LastAttempt { get; set; }
}
