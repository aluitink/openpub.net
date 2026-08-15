using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ActivityPub.Core.Interfaces;

namespace ActivityPub.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly IActivityPubRepository _repository;

    public IndexModel(IActivityPubRepository repository)
    {
        _repository = repository;
    }

    public void OnGet()
    {
    }
}
