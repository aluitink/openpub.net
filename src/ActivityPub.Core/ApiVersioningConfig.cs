using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;

namespace ActivityPub.Core;

/// <summary>
/// API Versioning configuration for ActivityPub endpoints
/// </summary>
public class ApiVersioningConfig
{
    /// <summary>
    /// Configures API versioning for the ActivityPub service
    /// </summary>
    public static void ConfigureApiVersioning(IServiceCollection services)
    {
        // Configure API versioning for ActivityPub endpoints
        services.AddApiVersioning(options =>
        {
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("api-version"),
                new MediaTypeApiVersionReader()
            );
        });
    }
}