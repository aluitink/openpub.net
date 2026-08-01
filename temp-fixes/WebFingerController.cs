using Microsoft.AspNetCore.Mvc;
using ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using ActivityPub.Core.Infrastructure.Telemetry;
using System.Diagnostics;

namespace ActivityPub.Core;

[ApiController]
[Route(".well-known/[controller]")]
public class WebFingerController : ControllerBase
{
    private readonly IWebFingerSource _webFingerSource;
    private readonly ILogger<WebFingerController> _logger;
    private readonly ActivityPubTelemetry _telemetry;

    public WebFingerController(IWebFingerSource webFingerSource, ILogger<WebFingerController> logger, ActivityPubTelemetry telemetry)
    {
        _webFingerSource = webFingerSource;
        _logger = logger;
        _telemetry = telemetry;
    }

    [HttpGet("webfinger")]
    public async Task<IActionResult> GetWebFinger(
        [FromQuery] string? resource,
        [FromQuery] string? rel)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("WebFinger request received for resource: {Resource}", resource);
        
        try
        {
            // Record the request for telemetry
            _telemetry.RecordWebFingerRequest();
            
            // Validate required parameters according to W3C WebFinger specification
            if (string.IsNullOrEmpty(resource))
            {
                _logger.LogWarning("WebFinger request missing resource parameter");
                _telemetry.RecordHttpRequestError(Request.Method, Request.Path, 400, new ArgumentException("Resource parameter is required"));
                return BadRequest(new { error = "resource parameter is required" });
            }

            // Validate that resource is a valid acct: URI
            if (!IsAcctUri(resource))
            {
                _logger.LogWarning("WebFinger request invalid resource format: {Resource}", resource);
                _telemetry.RecordHttpRequestError(Request.Method, Request.Path, 400, new ArgumentException("Invalid resource format"));
                return BadRequest(new { error = "Invalid resource format. Expected acct:username@美国domain" });
            }

            // Resolve the resource through the web finger source
            var resolvedResource = await _webFingerSource.GetWebFingerResourceAsync(resource);
            if (resolvedResource == null)
            {
                _logger.LogWarning("WebFinger resource not found: {Resource}", resource);
                _telemetry.RecordHttpRequestError(Request.Method, Request.Path, 404, new KeyNotFoundException("Resource not found"));
                return NotFound(new { error = "Resource not found" });
            }

            // Handle the resource according to WebFinger specification
            var subject = resource;
            var links = new List<object>();
            
            // Add self link to the ActivityPub endpoint
            var activityPubEndpoint = resolvedResource;
            links.Add(new
            {
                rel = "self",
                type = "application/activity+json",
                href = activityPubEndpoint
            });
            
            // Add additional links if rel parameter is provided
            if (!string.IsNullOrEmpty(rel))
            {
                // Add more links based on the relationship type if needed
                // This is a simplified implementation for now
            }

            // Return JRD (JSON Resource Descriptor) as per W3C specification
            var jrd = new
            {
                subject = subject,
                links = links.ToArray()
            };

            _logger.LogInformation("WebFinger request successful for resource: {Resource}", resource);
            _telemetry.RecordActivityProcessed("WebFinger");
            _telemetry.RecordWebFingerProcessingTime(stopwatch.ElapsedMilliseconds);
            _telemetry.RecordHttpRequestProcessed(Request.Method, Request.Path, 200, stopwatch.ElapsedMilliseconds);
            
            return Ok(jrd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebFinger request for resource: {Resource}", resource);
            _telemetry.RecordActivityError("WebFinger", ex);
            _telemetry.RecordHttpRequestError(Request.Method, Request.Path, 500, ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("WebFinger request completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private bool IsAcctUri(string resource)
    {
        // Validate that resource is a valid acct: URI format
        if (!resource.StartsWith("acct:"))
        {
            return false;
        }
        
        // Check that it has a username and domain
        var accountInfo = resource.Substring(5); // Remove "acct:" prefix
        var parts = accountInfo.Split('@');
        return parts.Length >= 2 && !string.IsNullOrEmpty(parts[0]) && !string.IsNullOrEmpty(parts[1]);
    }
}