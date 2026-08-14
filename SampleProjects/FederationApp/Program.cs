using ActivityPub.Core;
using FederationApp;
using FederationApp.Federation;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddControllers();
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
    options.EnableFederation = true;
});

builder.AddFederationApp();

var app = builder.Build();

app.UseRouting();

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();
