using ActivityPub.Core;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;
using static System.Math;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();

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

app.MapPost("/demo/actors", async (ActivityPubDbContext db, string username) =>
{
    var keyService = app.Services.GetRequiredService<IKeyGenerationService>();
    var keys = keyService.GenerateRSAKeyPair();
    var privateKey = keys.privateKeyPem;
    var publicKey = keys.publicKeyPem;
    
    var actor = new ActorEntity
    {
        Username = username,
        JsonData = $"{{\"publicKey\":\"{publicKey}\"}}"
    };
    
    await db.Actors.AddAsync(actor);
    await db.SaveChangesAsync();
    
    return Results.Created($"/actors/{actor.Id}", actor);
});

app.MapPost("/demo/activities", async (ActivityPubDbContext db, string activityId, string jsonData) =>
{
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

app.Run();

public class ActivityHub : Hub
{
    public async Task BroadcastActivity(string activityJson)
    {
        await Clients.All.SendAsync("ReceiveActivity", activityJson);
    }
}
