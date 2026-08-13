using ActivityPub.Core;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static System.Math;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddActivityPub(options =>
{
    options.Domain = "localhost";
    options.UserPath = "/users";
});

var app = builder.Build();

app.UseRouting();
app.UseStaticFiles();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<ActivityHub>("/activityHub");
});

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapGet("/demo/keys", async (IKeyGenerationService keyService) =>
{
    var (privateKey, publicKey) = keyService.GenerateRSAKeyPair();
    return Results.Ok(new
    {
        PrivateKey = privateKey,
        PublicKey = publicKey
    });
});

app.MapGet("/demo/actors", async (ActivityPubDbContext db) =>
{
    var actors = await db.Actors.ToListAsync();
    return Results.Ok(actors);
});

app.MapPost("/demo/actors", async (ActivityPubDbContext db, HttpContext context) =>
{
    var keyService = app.Services.GetRequiredService<IKeyGenerationService>();
    var keys = keyService.GenerateRSAKeyPair();
    var privateKey = keys.privateKeyPem;
    var publicKey = keys.publicKeyPem;
    
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var username = requestBody.Trim('"');
    
    var actor = new ActorEntity
    {
        Username = username,
        JsonData = $"{{\"publicKey\":\"{publicKey}\"}}"
    };
    
    await db.Actors.AddAsync(actor);
    await db.SaveChangesAsync();
    
    return Results.Created($"/actors/{actor.Id}", actor);
});

app.MapPost("/demo/activities", async (ActivityPubDbContext db, HttpContext context) =>
{
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);
    var activityId = data?.GetValueOrDefault("activityId") ?? "";
    var jsonData = data?.GetValueOrDefault("jsonData") ?? "";
    
    var activity = new ActivityEntity
    {
        ActivityId = activityId,
        JsonData = jsonData
    };
    
    await db.Activities.AddAsync(activity);
    await db.SaveChangesAsync();
    
    var hubContext = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<ActivityHub>>();
    await hubContext.Clients.All.SendAsync("ReceiveActivity", jsonData);
    
    return Results.Created($"/activities/{activity.Id}", activity);
});

app.MapGet("/demo/status", () =>
{
    return Results.Ok(new
    {
        Service = "ActivityPub Demo",
        Version = "1.0.0",
        Status = "Running"
    });
});

app.MapGet("/demo/activities/paginated", async (ActivityPubDbContext db, int page = 1, int pageSize = 10) =>
{
    var activities = await db.Activities
        .OrderByDescending(a => a.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
    
    var total = await db.Activities.CountAsync();
    
    return Results.Ok(new
    {
        Data = activities,
        Page = page,
        PageSize = pageSize,
        TotalItems = total,
        TotalPages = (int)Ceiling((double)total / pageSize)
    });
});

app.MapGet("/demo/templates", () =>
{
    var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "templates");
    var templatesJsonPath = Path.Combine(templateDir, "templates.json");
    
    if (File.Exists(templatesJsonPath))
    {
        var json = File.ReadAllText(templatesJsonPath);
        return Results.Content(json, "application/json");
    }
    
    return Results.NotFound();
});

app.MapGet("/demo/templates/{templateId}", (string templateId) =>
{
    var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "templates");
    var templatePath = Path.Combine(templateDir, $"{templateId}.json");
    
    if (File.Exists(templatePath))
    {
        var json = File.ReadAllText(templatePath);
        return Results.Content(json, "application/json");
    }
    
    return Results.NotFound();
});

app.Run();

public class ActivityHub : Hub
{
    public async Task BroadcastActivity(string activityJson)
    {
        await Clients.All.SendAsync("ReceiveActivity", activityJson);
    }
}
