using ActivityPub.Core;
using ActivityPub.Core.Implementations;
using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Services;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

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

        builder.Services.AddActivityPub(configureOptions: null, configureDbContext: opts =>
            opts.UseSqlite(builder.Configuration.GetConnectionString("ActivityPubConnection") ??
                "Data Source=fediblog_ap.db"));
        builder.Services.AddRazorPages();
        builder.Services.AddControllersWithViews();
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

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }
}
