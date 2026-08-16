using ActivityPub.Core;
using ActivityPub.Core.Options;
using ActivityPub.Core.Services;
using ActivityPub.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace FederationApp.Federation;

[ApiController]
[Route("api/federation")]
public class FederationController : ControllerBase
{
    private readonly FederationService _federationService;
    private readonly InstanceManager _instanceManager;

    public FederationController(FederationService federationService, InstanceManager instanceManager)
    {
        _federationService = federationService;
        _instanceManager = instanceManager;
    }

    [HttpGet("instances")]
    public async Task<ActionResult<List<InstanceInfo>>> GetInstances()
    {
        return Ok(await _instanceManager.GetInstancesAsync());
    }

    [HttpPost("instances")]
    public async Task<ActionResult> AddInstance([FromBody] InstanceInfo instance)
    {
        await _instanceManager.AddInstanceAsync(instance);
        return Ok(instance);
    }

    [HttpDelete("instances/{domain}")]
    public async Task<ActionResult> RemoveInstance(string domain)
    {
        await _instanceManager.RemoveInstanceAsync(domain);
        return NoContent();
    }

    [HttpPost("send")]
    public async Task<ActionResult> SendActivity([FromBody] SendActivityRequest request)
    {
        var success = await _federationService.SendActivityToInstanceAsync(request.ActivityJson, request.InstanceDomain);
        return Ok(new { success });
    }

    [HttpGet("delivery/status")]
    public async Task<ActionResult> GetDeliveryStatus()
    {
        return Ok(await _federationService.GetDeliveryStatusAsync());
    }
}

public class SendActivityRequest
{
    public string ActivityJson { get; set; } = string.Empty;
    public string InstanceDomain { get; set; } = string.Empty;
}
