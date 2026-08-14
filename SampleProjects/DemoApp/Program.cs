using DemoApp.Routing;
using DemoApp.Services;
using ActivityPub.Core;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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

var app = builder.Build();

app.UseRouting();
app.UseStaticFiles();
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
