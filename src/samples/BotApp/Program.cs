using ActivityPub.Core;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Services;
using BotApp.Bot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var activityPubSection = config.GetSection("ActivityPub");
var botSection = config.GetSection("Bot");

builder.Configuration.AddConfiguration(config);

builder.Services.AddControllers();
builder.Services.AddActivityPub(options =>
{
    activityPubSection.Bind(options);
});

builder.Services.AddDbContext<ActivityPubDbContext>(options =>
    options.UseInMemoryDatabase("BotAppDb"));

builder.Services.Configure<ActivityPubOptions>(activityPubSection);
builder.Services.Configure<BotOptions>(botSection);

builder.Services.AddSingleton<AutoResponder>();
builder.Services.AddHostedService<RelayService>();

builder.Services.AddScoped<IKeyGenerationService, KeyGenerationService>();
builder.Services.AddScoped<IFederationDiscoveryService, FederationDiscoveryService>();
builder.Services.AddScoped<IOutboundActivityService, OutboundActivityService>();
builder.Services.AddScoped<IActivityValidationService, ActivityValidationService>();

builder.Services.AddSingleton<IActivityPubRepository, EFCoreActivityPubRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();

app.MapControllers();

app.MapGet("/", () => Results.Text("ActivityPub Bot App is running!"));

app.MapGet("/health", async (ILogger<Program> logger) =>
{
    logger.LogInformation("Health check performed");
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
});

app.MapPost("/inbox", async (Activity activity, AutoResponder autoResponder, RelayService relayService, ILogger<Program> logger) =>
{
    logger.LogInformation("Received activity: {ActivityType} from {ActorId}", activity.Type, activity.ActorId);

    if (activity.Type == "Follow")
    {
        await autoResponder.HandleActivityAsync(activity);
    }
    else if (activity.Type == "Create")
    {
        await autoResponder.HandleActivityAsync(activity);

        var botOptions = new BotOptions();
        botSection.Bind(botOptions);

        if (botOptions.RelayEnabled)
        {
            await relayService.RelayToFollowersAsync(activity);
        }
    }

    return Results.Ok(new { status = "received" });
});

app.MapGet("/users/bot", async (ActivityPubOptions options) =>
{
    var botActor = new ActivityPub.Core.Models.Actor
    {
        Id = $"{options.Domain}/users/bot",
        Type = "Person",
        PreferredUsername = "bot",
        Inbox = $"{options.Domain}/inbox",
        Outbox = $"{options.Domain}/users/bot/outbox",
        Followers = $"{options.Domain}/users/bot/followers",
        Following = $"{options.Domain}/users/bot/following",
        Summary = "A simple ActivityPub bot with auto-respond and relay functionality",
        Published = DateTime.UtcNow
    };

    return Results.Json(botActor);
});

app.Run();

public partial class Program;

public class BotOptions
{
    public string Username { get; set; } = "bot";
    public bool AutoRespondEnabled { get; set; } = true;
    public bool RelayEnabled { get; set; } = true;
    public int RelayIntervalSeconds { get; set; } = 30;
}
