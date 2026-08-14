using DemoApp.Routing;
using DemoApp.Services;
using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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

builder.Services.AddSingleton<IKeyGenerationService, KeyService>();
builder.Services.AddScoped<IActorService, ActorService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddHostedService<QueueProcessorBackgroundService>();
builder.Services.AddSingleton<PerformanceMetricsService>();

builder.Services.AddSingleton<RateLimiterService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPFilterService>();
builder.Services.AddSingleton<AuditLogger>();

var app = builder.Build();

app.UseRouting();
app.UseStaticFiles();

app.UseMiddleware<RateLimitingMiddleware>(new ActivityPub.Core.Middleware.RateLimitOptions
{
    Window = TimeSpan.FromMinutes(1),
    MaxRequests = 100
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<ActivityHub>("/activityHub");
});

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
                    .Take(100)
                    .ToListAsync();

                if (pendingItems.Any())
                {
                    _logger.LogInformation("Processed {Count} items", pendingItems.Count);
                    _metrics.IncrementProcessedItems(pendingItems.Count);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
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
