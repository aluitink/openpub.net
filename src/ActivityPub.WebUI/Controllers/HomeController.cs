using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    [Route("about")]
    public IActionResult About() => View();

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        var errorCode = feature?.Error.HResult;
        var model = new ErrorViewModel
        {
            StatusCode = MapHttpStatus(errorCode),
            IsDevelopment = false
        };
        return View(model);
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public new IActionResult NotFound()
    {
        return View(new ErrorViewModel
        {
            StatusCode = 404,
            Title = "Page Not Found",
            Message = "The page you're looking for doesn't exist or has been moved.",
            IsDevelopment = false
        });
    }

    static int MapHttpStatus(int? hresult)
    {
        if (hresult == null) return 500;
        return hresult.Value switch
        {
            unchecked((int)0x80070002) => 404,
            unchecked((int)0x80070005) => 403,
            _ => 500
        };
    }
}

public class ErrorViewModel
{
    public int StatusCode { get; set; } = 500;
    public string Title { get; set; } = "An Error Occurred";
    public string Message { get; set; } = "Sorry, something went wrong. Please try again later.";
    public bool IsDevelopment { get; set; }
}
