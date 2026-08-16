using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
