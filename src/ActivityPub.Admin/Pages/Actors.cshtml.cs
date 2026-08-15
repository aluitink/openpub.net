using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ActivityPub.Core.Models;

namespace ActivityPub.Admin.Pages;

public class ActorsModel : PageModel
{
    public ICollection<Actor>? Actors { get; private set; }

    public void OnGet()
    {
        Actors = new List<Actor>();
    }
}
