using DemoApp.Routing;
using DemoApp.Services;
using DemoApp.Services.OAuth2;
using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Middleware;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddActivityPub(options =>
{
    options.Domain = "localhost";
    options.UserPath = "/users";
});

builder.Services.AddDbContext<ActivityPubDbContext>(options =>
    options.UseInMemoryDatabase("ActivityPubDemo"));

builder.Services.AddWebhookServices();

builder.Services.AddSingleton<IKeyGenerationService, KeyService>();
builder.Services.AddScoped<IActorService, ActorService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddHostedService<QueueProcessorBackgroundService>();
builder.Services.AddHostedService<WebhookDeliveryBackgroundService>();
builder.Services.AddSingleton<PerformanceMetricsService>();

builder.Services.AddSingleton<RateLimiterService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPFilterService>();
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton<IOAuth2Service, OAuth2Service>();
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

var app = builder.Build();

app.UseResponseCaching();
app.UseSecurityHeaders();
app.UseRateLimiting(options =>
{
    options.Window = TimeSpan.FromMinutes(1);
    options.MaxRequests = 100;
});
app.UseRouting();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<ActivityHub>("/activityHub");

app.MapEndpoints();

app.Run();

public partial class Program;

public class QueueItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class ActivityPubConfig
{
    public string? Domain { get; set; }
    public string? UserPath { get; set; }
    public int Port { get; set; }
}

public class LoggingConfig
{
    public Dictionary<string, string>? LogLevel { get; set; }
}

public class ActivityHub : Hub
{
    public async Task BroadcastActivity(string activityJson)
    {
        await Clients.All.SendAsync("ReceiveActivity", activityJson);
    }
}

public class QueueProcessorBackgroundService : BackgroundService
{
    private readonly ILogger<QueueProcessorBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly PerformanceMetricsService _metrics;

    public QueueProcessorBackgroundService(
        ILogger<QueueProcessorBackgroundService> logger,
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        PerformanceMetricsService metrics)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _cache = cache;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue processor background service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
                
                var pendingItems = await dbContext.Actors
                    .OrderBy(a => a.Username)
                    .Take(50)
                    .ToListAsync();

                if (pendingItems.Any())
                {
                    _logger.LogInformation("Processed {Count} items", pendingItems.Count);
                    _metrics.IncrementProcessedItems(pendingItems.Count);
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queue");
                _metrics.IncrementErrorCount();
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        _logger.LogInformation("Queue processor background service stopping");
    }
}

public class PerformanceMetricsService
{
    private readonly object _lock = new();
    private int _totalRequests;
    private int _errorCount;
    private int _processedItems;
    private readonly Dictionary<string, long> _endpointCounts = new();
    private readonly Dictionary<string, TimeSpan> _endpointTimes = new();

    public void RecordRequest(string endpoint, TimeSpan duration)
    {
        lock (_lock)
        {
            _totalRequests++;
            if (_endpointCounts.ContainsKey(endpoint))
            {
                _endpointCounts[endpoint]++;
                _endpointTimes[endpoint] = TimeSpan.FromMilliseconds(
                    (_endpointTimes[endpoint].TotalMilliseconds * (_endpointCounts[endpoint] - 1) + duration.TotalMilliseconds) / _endpointCounts[endpoint]);
            }
            else
            {
                _endpointCounts[endpoint] = 1;
                _endpointTimes[endpoint] = duration;
            }
        }
    }

    public void IncrementErrorCount()
    {
        lock (_lock)
        {
            _errorCount++;
        }
    }

    public void IncrementProcessedItems(int count)
    {
        lock (_lock)
        {
            _processedItems += count;
        }
    }

    public Dictionary<string, object> GetMetrics()
    {
        lock (_lock)
        {
            return new Dictionary<string, object>
            {
                { "totalRequests", _totalRequests },
                { "errorCount", _errorCount },
                { "processedItems", _processedItems },
                { "endpoints", _endpointCounts },
                { "averageEndpointTimes", _endpointTimes }
            };
        }
    }
}

[ApiController]
[Route("api/demo/instances")]
public class MultiInstanceController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MultiInstanceController> _logger;

    public MultiInstanceController(IMemoryCache cache, ILogger<MultiInstanceController> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetInstances()
    {
        var instances = _cache.Get<List<InstanceConfig>>("Instances") ?? new List<InstanceConfig>();
        var currentId = _cache.Get<string>("CurrentInstanceId");
        
        return Ok(new
        {
            instances = instances.Select(i => new
            {
                id = i.Id,
                name = i.Name,
                url = i.Url,
                defaultActor = i.DefaultActor,
                status = i.Status,
                createdAt = i.CreatedAt
            }),
            currentId = currentId
        });
    }

    [HttpPost]
    public IActionResult AddInstance([FromBody] InstanceConfig instance)
    {
        if (string.IsNullOrEmpty(instance.Id)) instance.Id = "instance-" + Guid.NewGuid().ToString().Substring(0, 8);
        if (string.IsNullOrEmpty(instance.Status)) instance.Status = "pending";
        instance.CreatedAt = DateTime.UtcNow;

        var instances = _cache.Get<List<InstanceConfig>>("Instances") ?? new List<InstanceConfig>();
        instances.Add(instance);
        _cache.Set("Instances", instances);

        _logger.LogInformation("Added instance: {InstanceId}", instance.Id);

        return Ok(new { success = true, instanceId = instance.Id });
    }

    [HttpDelete("{id}")]
    public IActionResult RemoveInstance(string id)
    {
        var instances = _cache.Get<List<InstanceConfig>>("Instances") ?? new List<InstanceConfig>();
        var instance = instances.FirstOrDefault(i => i.Id == id);
        
        if (instance == null)
            return NotFound(new { error = "Instance not found" });

        instances.Remove(instance);
        _cache.Set("Instances", instances);

        if (_cache.Get<string>("CurrentInstanceId") == id)
        {
            _cache.Set("CurrentInstanceId", instances.FirstOrDefault()?.Id);
        }

        _logger.LogInformation("Removed instance: {InstanceId}", id);

        return Ok(new { success = true });
    }

    [HttpPost("{id}/switch")]
    public IActionResult SwitchInstance(string id)
    {
        var instances = _cache.Get<List<InstanceConfig>>("Instances") ?? new List<InstanceConfig>();
        var instance = instances.FirstOrDefault(i => i.Id == id);

        if (instance == null)
            return NotFound(new { error = "Instance not found" });

        _cache.Set("CurrentInstanceId", id);

        _logger.LogInformation("Switched to instance: {InstanceId}", id);

        return Ok(new { success = true, instanceName = instance.Name });
    }

    [HttpGet("{id}/status")]
    public IActionResult GetInstanceStatus(string id)
    {
        var instances = _cache.Get<List<InstanceConfig>>("Instances") ?? new List<InstanceConfig>();
        var instance = instances.FirstOrDefault(i => i.Id == id);

        if (instance == null)
            return NotFound(new { error = "Instance not found" });

        var status = new InstanceStatus
        {
            Id = instance.Id,
            Name = instance.Name,
            Url = instance.Url,
            Status = instance.Status,
            ResponseTime = "N/A",
            Timestamp = DateTime.UtcNow.ToString("o"),
            Actors = instance.DefaultActor
        };

        return Ok(status);
    }

    [HttpGet("{id}/config")]
    public IActionResult GetInstanceConfig(string id)
    {
        var instances = _cache.Get<List<InstanceConfig>>("Instances") ?? new List<InstanceConfig>();
        var instance = instances.FirstOrDefault(i => i.Id == id);

        if (instance == null)
            return NotFound(new { error = "Instance not found" });

        var config = new
        {
            name = instance.Name,
            url = instance.Url,
            domain = instance.Url.Split('/').LastOrDefault(),
            port = 80,
            defaultActor = instance.DefaultActor,
            enabled = instance.Status == "active"
        };

        return Ok(config);
    }
}

public class InstanceConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string DefaultActor { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
}

public class InstanceStatus
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ResponseTime { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Actors { get; set; } = string.Empty;
}
