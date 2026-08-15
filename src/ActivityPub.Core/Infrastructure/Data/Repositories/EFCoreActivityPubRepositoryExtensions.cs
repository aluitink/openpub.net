using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ActivityPub.Core.Repositories;

public static class EFCoreActivityPubRepositoryExtensions
{
    public static IServiceCollection AddEFCoreActivityPubRepository(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ActivityPubDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IActivityPubRepository, EFCoreActivityPubRepository>();

        return services;
    }
}
