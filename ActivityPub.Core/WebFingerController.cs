using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.Core;

[ApiController]
[Route(".well-known/[controller]")]
public class WebFingerController : ControllerBase
{
    [HttpGet("webfinger")]
    public IActionResult GetWebFinger(
        [FromQuery] string? resource,
        [FromQuery] string? rel)
    {
        // Validate required parameters according to W3C WebFinger specification
        if (string.IsNullOrEmpty(resource))
        {
            return BadRequest(new { error = "resource parameter is required" });
        }

        // Handle the resource according to WebFinger specification
        var subject = resource;
        var links = new List<object>();
        
        // Add self link to the ActivityPub endpoint
        var activityPubEndpoint = GetActivityPubEndpoint(resource);
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
            // This is a simplified implementation
        }

        // Return JRD (JSON Resource Descriptor) as per W3C specification
        var jrd = new
        {
            subject = subject,
            links = links.ToArray()
        };

        return Ok(jrd);
    }

    private string GetActivityPubEndpoint(string resource)
    {
        // According to W3C WebFinger specification for ActivityPub:
        // The resource URI should resolve to an ActivityPub actor
        // We'll handle common formats like acct: usernames
        
        if (resource.StartsWith("acct:"))
        {
            // Extract username from acct:username@domain format
            var accountInfo = resource.Substring(5); // Remove "acct:" prefix
            var parts = accountInfo.Split('@');
            if (parts.Length >= 2)
            {
                var username = parts[0];
                var domain = parts[1];
                // Return standard ActivityPub user endpoint
                return $"/users/{username}";
            }
        }
        
        // For other resource types, return as-is or construct appropriate endpoint
        return resource;
    }
}