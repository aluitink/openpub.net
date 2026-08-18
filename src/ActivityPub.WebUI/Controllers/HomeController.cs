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

    // Re-executed by UseStatusCodePagesWithReExecute for any non-2xx status
    // code that isn't an unhandled exception. The original status code is
    // passed in via the `id` query string (e.g. /Home/StatusError?id=403).
    [HttpGet]
    [Route("Home/StatusError")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusError([FromQuery] int id = 404)
    {
        return View(BuildModel(id));
    }

    // Hidden alias kept so the conventional route /Home/NotFound still works.
    // It renders the same status-aware view as StatusError (defaulting to 404).
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult NotFound([FromQuery] int id = 404)
    {
        return View("StatusError", BuildModel(id));
    }

    // Direct, on-brand 403 page (reachable at /Home/Forbidden).
    [HttpGet]
    [Route("Home/Forbidden")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Forbidden()
    {
        return View("StatusError", BuildModel(403));
    }

    static ErrorViewModel BuildModel(int statusCode)
    {
        return statusCode switch
        {
            403 => new ErrorViewModel
            {
                StatusCode = 403,
                Title = "Access Denied",
                Message = "You don't have permission to view this page."
            },
            404 => new ErrorViewModel
            {
                StatusCode = 404,
                Title = "Page Not Found",
                Message = "The page you're looking for doesn't exist or has been moved."
            },
            410 => new ErrorViewModel
            {
                StatusCode = 410,
                Title = "Gone",
                Message = "This content is no longer available."
            },
            429 => new ErrorViewModel
            {
                StatusCode = 429,
                Title = "Too Many Requests",
                Message = "You're sending requests too quickly. Please slow down and try again shortly."
            },
            500 => new ErrorViewModel
            {
                StatusCode = 500,
                Title = "Something Went Wrong",
                Message = "An unexpected error occurred. Please try again later."
            },
            502 => new ErrorViewModel
            {
                StatusCode = 502,
                Title = "Bad Gateway",
                Message = "The upstream service is unavailable. Please try again later."
            },
            503 => new ErrorViewModel
            {
                StatusCode = 503,
                Title = "Service Unavailable",
                Message = "The server is temporarily unable to handle your request. Please try again later."
            },
            504 => new ErrorViewModel
            {
                StatusCode = 504,
                Title = "Gateway Timeout",
                Message = "The upstream server took too long to respond. Please try again later."
            },
            _ => new ErrorViewModel
            {
                StatusCode = statusCode,
                Title = statusCode is >= 500 and < 600 ? "Something Went Wrong" : "Request Failed",
                Message = statusCode is >= 500 and < 600
                    ? "An unexpected error occurred. Please try again later."
                    : "Your request could not be processed. Please try again."
            }
        };
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
