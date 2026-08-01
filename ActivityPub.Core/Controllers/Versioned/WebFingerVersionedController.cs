using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;

namespace ActivityPub.Core.Controllers.Versioned;

/// <summary>
/// Versioned WebFinger controller to demonstrate API versioning
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class WebFingerVersionedController : ControllerBase
{
    private readonly IWebFingerSource _webFingerSource;

    public WebFingerVersionedController(IWebFingerSource webFingerSource)
    {
        _webFingerSource = webFingerSource ?? throw new ArgumentNullException(nameof(webFingerSource));
    }

    /// <summary>
    /// Gets WebFinger resource descriptor with versioning support
    /// </summary>
    /// <param name="resource">The resource identifier in acct: format</param>
    /// <returns>JSON Resource Descriptor (JRD) response</returns>
    [HttpGet("webfinger")]
    public async Task<IActionResult> GetWebFinger(
        [FromQuery(Name = "resource")] string? resource,
        [FromQuery(Name = "rel")] string? rel)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return BadRequest(new { error = "Missing required resource parameter" });
        }

        try
        {
            var jrd = await _webFingerSource.GetWebFingerAsync(resource);
            if (jrd == null)
            {
                return NotFound(new { error = "Resource not found" });
            }
            return Ok(jrd);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}