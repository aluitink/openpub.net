using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ActivityPub.Core.Models;

namespace ActivityPub.Admin.Pages;

public class ActivitiesModel : PageModel
{
    public ICollection<Activity>? Activities { get; private set; }

    public void OnGet()
    {
        Activities = new List<Activity>();
    }
}
