using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ActivityPub.Core;
using System.Reflection;

namespace ActivityPub.Core;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddActivityPub();
        builder.Services.AddControllers();
        
        // Configure API versioning
        ApiVersioningConfig.ConfigureApiVersioning(builder.Services);

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (builder.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseApiVersioning(); // Apply API versioning middleware
        app.MapControllers();

        app.Run();
    }
}