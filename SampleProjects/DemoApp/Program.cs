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
using Microsoft.Extensions.Configuration;
using System;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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

app.MapGet("/demo/config", (IConfiguration config) =>
{
    var activityPubSection = config.GetSection("ActivityPub").Get<ActivityPubConfig>();
    var loggingSection = config.GetSection("Logging").Get<LoggingConfig>();
    
    return Results.Ok(new
    {
        ActivityPub = activityPubSection,
        Logging = loggingSection,
        Timestamp = DateTime.UtcNow
    });
});

app.MapPost("/demo/config", async (HttpContext context) =>
{
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    
    if (string.IsNullOrWhiteSpace(requestBody))
        return Results.BadRequest("Invalid configuration");
    
    var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    
    File.WriteAllText(appSettingsPath, requestBody);
    
    return Results.Ok(new
    {
        Success = true,
        Message = "Configuration updated",
        Timestamp = DateTime.UtcNow
    });
});

app.MapGet("/demo/config/validation", (IConfiguration config) =>
{
    var errors = new List<string>();
    
    var domain = config["ActivityPub:Domain"];
    if (string.IsNullOrWhiteSpace(domain))
        errors.Add("Domain is required");
    
    return Results.Ok(new
    {
        Valid = !errors.Any(),
        Errors = errors
    });
});

app.MapGet("/demo/queues", () =>
{
    return Results.Ok(new
    {
        Outbound = new
        {
            Total = 0,
            Pending = 0,
            Processing = 0,
            Completed = 0,
            Failed = 0
        },
        Inbound = new
        {
            Total = 0,
            Pending = 0,
            Processing = 0,
            Completed = 0,
            Failed = 0
        },
        Items = Array.Empty<QueueItem>()
    });
});

app.MapPost("/demo/queues/retry", () =>
{
    return Results.Ok(new
    {
        Success = true,
        Retried = 0,
        Message = "No failed items to retry"
    });
});

app.MapPost("/demo/queues/clear", () =>
{
    return Results.Ok(new
    {
        Success = true,
        Message = "Queue cleared"
    });
});

app.MapPost("/demo/http-signature/sign", async (HttpContext context, IConfiguration config) =>
{
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);
    
    var keyId = data?.GetValueOrDefault("keyId") ?? "";
    var privateKeyPem = data?.GetValueOrDefault("privateKey") ?? "";
    var urlString = data?.GetValueOrDefault("url") ?? "";
    var method = data?.GetValueOrDefault("method") ?? "POST";
    
    var keyService = app.Services.GetRequiredService<IKeyGenerationService>();
    
    return Results.Ok(new
    {
        Success = !string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(privateKeyPem),
        KeyId = keyId,
        Algorithm = "rsa-sha256",
        Headers = new[] { "(request-target)", "host", "date", "digest", "content-length" },
        Timestamp = DateTime.UtcNow
    });
});

app.MapPost("/demo/http-signature/verify", async (HttpContext context) =>
{
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);
    
    var signature = data?.GetValueOrDefault("signature") ?? "";
    var signedHeaders = data?.GetValueOrDefault("signedHeaders") ?? "";
    
    return Results.Ok(new
    {
        Valid = !string.IsNullOrWhiteSpace(signature),
        Signature = signature,
        SignedHeaders = signedHeaders,
        Timestamp = DateTime.UtcNow
    });
});

app.MapGet("/demo/http-signature/generate-test", (IKeyGenerationService keyService) =>
{
    var (privateKey, publicKey) = keyService.GenerateRSAKeyPair();
    
    return Results.Ok(new
    {
        KeyId = "test-key-" + Guid.NewGuid().ToString().Substring(0, 8),
        PrivateKey = privateKey,
        PublicKey = publicKey,
        ExampleHeaders = new Dictionary<string, string>
        {
            { "(request-target)", "post /api/test" },
            { "host", "localhost:8080" },
            { "date", DateTime.UtcNow.ToString("R") },
            { "digest", "SHA-256=47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=" },
            { "content-length", "0" }
        }
    });
});

app.MapGet("/demo/federation/discover", async (IConfiguration config, HttpClient httpClient) =>
{
    var actorUrl = config["Query:ActorUrl"] ?? "http://localhost:8080/users/test";
    
    return Results.Ok(new
    {
        ActorUrl = actorUrl,
        Endpoints = new
        {
            Inbox = $"{actorUrl}/inbox",
            Outbox = $"{actorUrl}/outbox",
            Followers = $"{actorUrl}/followers",
            Following = $"{actorUrl}/following"
        },
        Health = "online"
    });
});

app.MapGet("/demo/federation/webfinger", async (string resource, HttpClient httpClient) =>
{
    if (string.IsNullOrWhiteSpace(resource))
        return Results.BadRequest("Resource parameter required");
    
    return Results.Ok(new
    {
        Subject = $"acct:{resource}",
        Links = new[]
        {
            new { Rel = "self", Type = "application/activity+json", Href = $"http://localhost:8080/users/{resource}" },
            new { Rel = "avatar", Type = "image/png", Href = "http://localhost:8080/avatar.png" },
            new { Rel = "pubkey", Type = "key", Href = "http://localhost:8080/publickey" }
        }
    });
});

app.MapPost("/demo/service/simulate-receive", async (HttpContext context) =>
{
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    
    return Results.Ok(new
    {
        Success = true,
        Message = "Simulated activity received",
        ActivityId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow
    });
});

app.MapPost("/demo/service/simulate-send", async (HttpContext context) =>
{
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);
    
    var recipient = data?.GetValueOrDefault("recipient") ?? "";
    var activity = data?.GetValueOrDefault("activity") ?? "";
    
    return Results.Ok(new
    {
        Success = true,
        Recipient = recipient,
        ActivityId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow
    });
});

app.MapGet("/demo/protocol/validate", async (HttpContext context) =>
{
    var activityType = context.Request.Query["type"].ToString();
    
    return Results.Ok(new
    {
        Valid = !string.IsNullOrWhiteSpace(activityType),
        ActivityType = activityType,
        Errors = Array.Empty<string>(),
        Warnings = Array.Empty<string>()
    });
});

app.MapGet("/demo/explorer/activities", async (string actorUrl, HttpClient httpClient) =>
{
    if (string.IsNullOrWhiteSpace(actorUrl))
        return Results.BadRequest("Actor URL parameter required");
    
    try
    {
        var response = await httpClient.GetStringAsync(actorUrl);
        var activities = new
        {
            ActorUrl = actorUrl,
            Activities = new[]
            {
                new { Id = Guid.NewGuid().ToString(), Type = "Create", Content = "Sample activity from explorer" }
            }
        };
        
        return Results.Ok(activities);
    }
    catch
    {
        return Results.Ok(new
        {
            ActorUrl = actorUrl,
            Activities = Array.Empty<object>()
        });
    }
});

app.MapGet("/demo/explorer/trace", async (string actorUrl, HttpClient httpClient) =>
{
    if (string.IsNullOrWhiteSpace(actorUrl))
        return Results.BadRequest("Actor URL parameter required");
    
    return Results.Ok(new
    {
        ActorUrl = actorUrl,
        Chain = new[]
        {
            new { Id = Guid.NewGuid().ToString(), Type = "Follow", Timestamp = DateTime.UtcNow.ToString("o") },
            new { Id = Guid.NewGuid().ToString(), Type = "Accept", Timestamp = DateTime.UtcNow.ToString("o") }
        },
        TraceCompleted = true
    });
});

app.Run();

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
