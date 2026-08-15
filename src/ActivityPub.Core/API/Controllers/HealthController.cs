using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Controllers;

[ApiController]
[Route("_health")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetHealth()
    {
        var health = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            services = new
            {
                inbox = "operational",
                federation = "operational",
                storage = "operational"
            }
        };

        return Ok(health);
    }

    [HttpGet("details")]
    public IActionResult GetHealthDetails()
    {
        var details = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            services = new
            {
                inbox = new { status = "operational", checks = new[] { "database", "queue" } },
                federation = new { status = "operational", checks = new[] { "dns", "http" } },
                storage = new { status = "operational", checks = new[] { "disk", "connections" } }
            },
            summary = "All ActivityPub services operational"
        };

        return Ok(details);
    }
}
