using ActivityPub.Core;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Middleware;
using ActivityPub.Core.Services;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ActivityPub.Core.Repositories;

namespace ActivityPub.WebUI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ??
                "Data Source=fediblog.db"));

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/auth/login";
            options.LogoutPath = "/auth/logout";
            options.AccessDeniedPath = "/auth/login";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        // Register the custom Bearer-token scheme for API authentication while
        // keeping the Identity cookie scheme as the default (so existing
        // [Authorize] controllers and cookie sign-in keep working unchanged).
        builder.Services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ActivityPub.WebUI.Auth.BearerTokenAuthenticationHandler>(
                ActivityPub.WebUI.Auth.BearerTokenAuthConstants.SchemeName, null);

        builder.Services.AddActivityPub(configureOptions: null, configureDbContext: opts =>
            opts.UseSqlite(builder.Configuration.GetConnectionString("ActivityPubConnection") ??
                "Data Source=fediblog_ap.db"));
        builder.Services.AddRazorPages();
        builder.Services.AddControllersWithViews();
        builder.Services.AddResponseCaching();

        // API documentation (Swagger UI + OpenAPI JSON) for the local
        // Mastodon-shaped REST API under /api/v1.
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "ActivityPub.NET REST API",
                Version = "v1",
                Description = "Mastodon-compatible local REST API (/api/v1): statuses, accounts, " +
                              "timelines, application registration, and OAuth 2.0 (PKCE) for API auth.",
            });

            // Pick up XML doc comments from the controllers/DTOs.
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (System.IO.File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);

            // Advertise both authentication mechanisms the API accepts:
            // a Bearer access token (OAuth 2.0) and the cookie session.
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "OAuth 2.0 Bearer access token (from POST /api/v1/oauth/token).",
                Name = "Authorization",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
            });
            options.AddSecurityDefinition("Cookies", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "Authenticated browser cookie session.",
                Name = "Cookie",
                In = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            });
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        }
                    },
                    Array.Empty<string>()
                },
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Cookies",
                        }
                    },
                    Array.Empty<string>()
                },
            });
        });

        // API (Mastodon REST) rate limiting — configurable via the
        // "ApiRateLimit" section in appsettings.json.
        builder.Services.Configure<ActivityPub.Core.Options.ApiRateLimitOptions>(
            builder.Configuration.GetSection("ApiRateLimit"));

        // ActivityPub federation options — configurable via the "ActivityPub"
        // section in appsettings.json (e.g. EnableSignatureVerification,
        // RequireSignatures). Binds over the defaults set by AddActivityPub.
        builder.Services.Configure<ActivityPub.Core.Options.ActivityPubOptions>(
            builder.Configuration.GetSection("ActivityPub"));
        builder.Services.AddMemoryCache();

        // SignalR scale-out. When the Redis backplane is enabled
        // (ActivityPub:Realtime:Enabled in appsettings.json), hub messages are
        // broadcast across all instances via a shared Redis connection, and a
        // distributed (Redis-backed) rate limiter enforces per-connection limits
        // globally. Otherwise SignalR runs in single-process mode with an
        // in-memory rate limiter.
        var realtimeOpts = builder.Configuration
            .GetSection("ActivityPub:Realtime")
            .Get<ActivityPub.Core.Options.RealtimeOptions>()
            ?? new ActivityPub.Core.Options.RealtimeOptions();

        if (realtimeOpts.Enabled)
        {
            // Singleton IConnectionMultiplexer used by the distributed rate
            // limiter. The SignalR backplane below opens its own connection from
            // the same connection string (managed by the backplane's lifetime),
            // so both the backplane and rate limiting talk to the same Redis.
            builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(realtimeOpts.RedisConnection));

            // Enable the Redis backplane so hub messages broadcast on one
            // instance reach clients connected to any other instance.
            var serverBuilder = builder.Services.AddSignalR();
            serverBuilder.AddStackExchangeRedis(realtimeOpts.RedisConnection, redisOptions =>
            {
                redisOptions.Configuration = new StackExchange.Redis.ConfigurationOptions
                {
                    AbortOnConnectFail = false,
                    ChannelPrefix = StackExchange.Redis.RedisChannel.Literal(realtimeOpts.ChannelPrefix)
                };
            });

            builder.Services.AddSingleton<ActivityPub.WebUI.Hubs.IHubRateLimiter, ActivityPub.WebUI.Hubs.RedisHubRateLimiter>();
        }
        else
        {
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<ActivityPub.WebUI.Hubs.IHubRateLimiter, ActivityPub.WebUI.Hubs.InMemoryHubRateLimiter>();
        }

        builder.Services.AddScoped<ActivityPub.WebUI.Services.INotificationService, ActivityPub.WebUI.Services.SignalRNotificationService>();
        builder.Services.AddScoped<ActivityPub.WebUI.Services.IPushNotificationService, ActivityPub.WebUI.Services.PushNotificationService>();
        builder.Services.AddScoped<ActivityPub.WebUI.Services.IAuditLogService, ActivityPub.WebUI.Services.AuditLogService>();
        builder.Services.AddScoped<ActivityPub.WebUI.Services.IUserReportService, ActivityPub.WebUI.Services.UserReportService>();

        // Webhook delivery for external integrations: enqueue + poll a durable,
        // DB-backed delivery queue. The background service pumps pending
        // deliveries (HMAC-signed POST/PUT to each subscriber endpoint).
        builder.Services.AddWebhookServices();
        builder.Services.AddHostedService<ActivityPub.Core.Services.WebhookDeliveryBackgroundService>();

        builder.Services.AddHttpClient<IWebFingerService, WebFingerService>();
        builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteOptions>(options =>
        {
            options.ConstraintMap.Add("apiVersion", typeof(ActivityPub.WebUI.DummyRouteConstraint));
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }
        else
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStatusCodePagesWithReExecute("/Home/NotFound", "?id={0}");

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseResponseCaching();
        app.UseAuthorization();

        // Verify HTTP signatures on inbound ActivityPub activity deliveries
        // (POST to inbox endpoints). Gated by ActivityPubOptions
        // .EnableSignatureVerification / .RequireSignatures (see the
        // "ActivityPub" config section).
        app.UseMiddleware<ActivityPub.Core.Middleware.HttpSignatureMiddleware>();

        app.UseApiRateLimiting();
        app.MapStaticAssets();

        app.UseRateLimiting(options =>
        {
            options.Window = TimeSpan.FromMinutes(1);
            options.MaxRequests = 20;
            options.Paths = new[] { "/compose/post", "/follow/follow" };
        });

        app.MapControllers();

        // Swagger UI + OpenAPI JSON for the REST API. The OpenAPI document is
        // public (it describes the API contract); the Swagger UI at /swagger
        // links to it. Both live outside /api/v1, so they are exempt from the
        // API rate limiter.
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            // Swashbuckle serves the OpenAPI JSON at /swagger/{doc}/swagger.json.
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "REST API v1");
            options.DocumentTitle = "ActivityPub.NET REST API";
        });

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.MapHub<ActivityPub.WebUI.Hubs.NotificationHub>("/notifications/ws");

        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<ActivityPubDbContext>().Database.EnsureCreated();
        }

        app.Run();
    }
}
