using ActivityPub.Core.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ActivityPub.Core.Repositories;

/// <summary>
/// Extension methods that configure an Entity Framework context for the relational
/// database provider selected in <see cref="DatabaseOptions"/>. The same context
/// definition (and the same <c>OnModelCreating</c>) works against both SQLite and
/// PostgreSQL; only the provider-specific connection handling differs.
/// </summary>
public static class DbProviderExtensions
{
    /// <summary>
    /// Configures the given context options builder for the specified provider and
    /// connection string. For <see cref="DatabaseProvider.Sqlite"/> the connection
    /// string is a file path (e.g. "Data Source=app.db"); for
    /// <see cref="DatabaseProvider.Postgresql"/> it is a full Npgsql connection string
    /// (e.g. "Host=localhost;Database=ap;Username=ap;Password=ap").
    /// </summary>
    public static DbContextOptionsBuilder ConfigureDatabaseProvider(
        this DbContextOptionsBuilder builder,
        DatabaseProvider provider,
        string connectionString)
    {
        return provider switch
        {
            DatabaseProvider.Postgresql => builder.UseNpgsql(connectionString),
            _ => builder.UseSqlite(connectionString)
        };
    }

    /// <summary>
    /// Registers <see cref="ActivityPubDbContext"/> using the federation connection
    /// string and provider resolved from <see cref="ActivityPubOptions.Database"/>.
    /// This is the recommended way to wire the federation context so that switching
    /// providers is a configuration-only change.
    /// </summary>
    public static IServiceCollection AddActivityPubDbContext(
        this IServiceCollection services,
        IOptions<ActivityPubOptions> options)
    {
        var dbOptions = options.Value.Database;
        services.AddDbContext<ActivityPubDbContext>(ctxOptions =>
            ctxOptions.ConfigureDatabaseProvider(
                dbOptions.Provider,
                dbOptions.GetFederationConnectionString()));
        return services;
    }

    /// <summary>
    /// Registers a generic <typeparamref name="TContext"/> using the specified
    /// provider and connection string. Used to configure the Identity
    /// (<c>ApplicationDbContext</c>) context for the selected provider.
    /// </summary>
    public static IServiceCollection AddConfiguredDbContext<TContext>(
        this IServiceCollection services,
        DatabaseProvider provider,
        string connectionString)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(ctxOptions =>
            ctxOptions.ConfigureDatabaseProvider(provider, connectionString));
        return services;
    }
}
