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
        builder.Services.AddMemoryCache();
        builder.Services.AddSignalR();
        builder.Services.AddScoped<ActivityPub.WebUI.Services.INotificationService, ActivityPub.WebUI.Services.SignalRNotificationService>();
        builder.Services.AddScoped<ActivityPub.WebUI.Services.IPushNotificationService, ActivityPub.WebUI.Services.PushNotificationService>();
        builder.Services.AddScoped<ActivityPub.WebUI.Services.IAuditLogService, ActivityPub.WebUI.Services.AuditLogService>();
        builder.Services.AddScoped<ActivityPub.WebUI.Services.IUserReportService, ActivityPub.WebUI.Services.UserReportService>();
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
        app.MapStaticAssets();

        app.UseRateLimiting(options =>
        {
            options.Window = TimeSpan.FromMinutes(1);
            options.MaxRequests = 20;
            options.Paths = new[] { "/compose/post", "/follow/follow" };
        });

        app.MapControllers();

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
