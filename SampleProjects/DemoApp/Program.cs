using ActivityPub.Core;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddActivityPub(options =>
{
    options.Domain = "localhost";
    options.UserPath = "/users";
});

var app = builder.Build();

app.UseRouting();
app.UseStaticFiles();

app.MapControllers();

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

app.Run();
