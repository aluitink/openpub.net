using System.Reflection;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ActivityPub.Core.Migration;

/// <summary>
/// Progress report emitted by <see cref="DatabaseMigrationService"/> while a
/// database migration is in flight.
/// </summary>
public sealed class MigrationProgress
{
    /// <summary>The entity type name being migrated (e.g. "ActorEntity").</summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>The number of rows already copied for this entity.</summary>
    public long RowsCopied { get; init; }

    /// <summary>
    /// The total number of rows in the source for this entity (0 until the first
    /// read completes and the count is known).
    /// </summary>
    public long SourceRowCount { get; init; }

    /// <summary>True when this entity's rows have all been copied.</summary>
    public bool IsComplete { get; init; }
}

/// <summary>
/// The outcome of a single entity's migration, used to build a final summary.
/// </summary>
public sealed record EntityMigrationResult(
    string EntityType,
    long RowsCopied,
    bool Skipped,
    string? Reason = null);

/// <summary>
/// The aggregate outcome of migrating one context (federation or identity).
/// </summary>
public sealed class ContextMigrationResult
{
    public string ContextName { get; init; } = string.Empty;
    public List<EntityMigrationResult> Entities { get; init; } = new();
    public long TotalRowsCopied => Entities.Sum(e => e.RowsCopied);
    public bool Success { get; init; }
}

/// <summary>
/// Copies all data from a source database to a target database for a given
/// Entity Framework context. The source is typically SQLite (the historical
/// default) and the target is typically PostgreSQL, but the service is
/// provider-agnostic on both ends and can also copy SQLite to SQLite (used by
/// the test suite, since no PostgreSQL server is available in CI).
///
/// The migration preserves primary keys (original IDs are reused) and inserts
/// entities in a dependency-safe order so that foreign keys resolve. It is
/// idempotent in the sense that it copies whatever rows exist in the source; it
/// does not delete target rows that are absent from the source.
/// </summary>
public sealed class DatabaseMigrationService
{
    /// <summary>
    /// Maximum number of rows to copy per <c>SaveChanges</c> call for a given
    /// entity. Keeps memory usage and transaction size bounded for large tables.
    /// </summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// Migrates the <see cref="ActivityPubDbContext"/> (federation data) from the
    /// source to the target database.
    /// </summary>
    public async Task<ContextMigrationResult> MigrateFederationAsync(
        DatabaseProvider sourceProvider, string sourceConnectionString,
        DatabaseProvider targetProvider, string targetConnectionString,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await MigrateAsync<ActivityPubDbContext>(
            "ActivityPubDbContext",
            sourceProvider, sourceConnectionString,
            targetProvider, targetConnectionString,
            progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Migrates a generic context (e.g. the Identity <c>ApplicationDbContext</c>)
    /// from the source to the target database.
    /// </summary>
    public Task<ContextMigrationResult> MigrateContextAsync<TContext>(
        string contextName,
        DatabaseProvider sourceProvider, string sourceConnectionString,
        DatabaseProvider targetProvider, string targetConnectionString,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        return MigrateAsync<TContext>(
            contextName,
            sourceProvider, sourceConnectionString,
            targetProvider, targetConnectionString,
            progress, cancellationToken);
    }

    private async Task<ContextMigrationResult> MigrateAsync<TContext>(
        string contextName,
        DatabaseProvider sourceProvider, string sourceConnectionString,
        DatabaseProvider targetProvider, string targetConnectionString,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var results = new List<EntityMigrationResult>();
        long totalCopied = 0;

        // Ensure the target schema exists (the app uses EnsureCreated, not EF
        // migrations, so this is the correct way to materialize the schema).
        await using (var target = CreateContext<TContext>(targetProvider, targetConnectionString))
        {
            target.ChangeTracker.AutoDetectChangesEnabled = false;
            await target.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

            var entityTypeOrder = OrderEntityTypes(target.Model);

            foreach (var entityType in entityTypeOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entityTypeName = entityType.ShortName();

                long copied = 0;

                await using var source = CreateContext<TContext>(sourceProvider, sourceConnectionString);
                source.ChangeTracker.AutoDetectChangesEnabled = false;

                // The source database may have been created by an older version of
                // the application and therefore be missing tables that the current
                // model defines (e.g. tables added in a recent release). Such
                // entities are skipped rather than aborting the whole migration;
                // the target still receives its full current schema via
                // EnsureCreated above.
                List<object> allRows;
                try
                {
                    // Read every row for this entity in a single query. The source is
                    // read-only during the migration, so a one-shot read is stable.
                    allRows = await ReadAllAsync<TContext>(source, entityType, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (TryClassifySchemaDrift(ex, entityType, out var driftReason))
                {
                    // The source was created by an older schema and is missing a
                    // table or column the current model expects. Skip this entity;
                    // the target still receives the full current schema.
                    results.Add(new EntityMigrationResult(
                        entityTypeName, 0, Skipped: true, Reason: driftReason));
                    continue;
                }

                long sourceCount = allRows.Count;

                for (var i = 0; i < sourceCount; i += BatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batch = allRows.GetRange(i, Math.Min(BatchSize, (int)(sourceCount - i)));

                    target.ChangeTracker.Clear();
                    // The entities are fully materialized from the source, so every
                    // property (including primary keys and value-generated columns)
                    // already carries a value. EF reuses those values in the INSERT
                    // and does not regenerate them, which preserves original IDs.
                    InsertBatch(target, entityType.ClrType, batch);

                    await target.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    copied += batch.Count;
                    progress?.Report(new MigrationProgress
                    {
                        EntityType = entityTypeName,
                        RowsCopied = copied,
                        SourceRowCount = sourceCount,
                        IsComplete = copied >= sourceCount
                    });
                }

                totalCopied += copied;
                results.Add(new EntityMigrationResult(entityTypeName, copied, Skipped: false));
            }
        }

        return new ContextMigrationResult
        {
            ContextName = contextName,
            Entities = results,
            Success = true
        };
    }

    /// <summary>
    /// Creates a context instance configured for the specified provider and
    /// connection string.
    /// </summary>
    public static TContext CreateContext<TContext>(DatabaseProvider provider, string connectionString)
        where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.ConfigureDatabaseProvider(provider, connectionString);
        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }

    /// <summary>
    /// Orders the entity types of a model so that dependents come after their
    /// dependencies, ensuring foreign keys resolve during bulk insert.
    /// </summary>
    public static IEnumerable<IEntityType> OrderEntityTypes(IModel model)
    {
        var entityTypes = model.GetEntityTypes().ToList();
        var visited = new HashSet<IEntityType>();
        var result = new List<IEntityType>();

        // Build a dependency graph: an entity depends on any entity it has a
        // required/optional FK reference to.
        var dependencies = new Dictionary<IEntityType, List<IEntityType>>();
        foreach (var et in entityTypes)
        {
            var deps = new List<IEntityType>();
            foreach (var fk in et.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                if (!ReferenceEquals(principal, et))
                {
                    deps.Add(principal);
                }
            }
            dependencies[et] = deps;
        }

        void Visit(IEntityType et)
        {
            if (!visited.Add(et))
            {
                return;
            }
            foreach (var dep in dependencies[et])
            {
                Visit(dep);
            }
            result.Add(et);
        }

        foreach (var et in entityTypes)
        {
            Visit(et);
        }

        return result;
    }

    private static async Task<List<object>> ReadAllAsync<TContext>(
        TContext context, IEntityType entityType, CancellationToken ct)
        where TContext : DbContext
    {
        var method = typeof(DatabaseMigrationService)
            .GetMethod(nameof(ReadAllFor), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType.ClrType);
        var task = (Task<List<object>>)method.Invoke(null, new object[] { context, ct })!;
        return await task.ConfigureAwait(false);
    }

    private static async Task<List<object>> ReadAllFor<T>(DbContext context, CancellationToken ct)
        where T : class
    {
        var rows = await context.Set<T>().ToListAsync(ct).ConfigureAwait(false);
        return rows.Cast<object>().ToList();
    }

    private static void InsertBatch(DbContext context, Type entityType, List<object> batch)
    {
        var method = typeof(DatabaseMigrationService)
            .GetMethod(nameof(InsertFor), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);
        method.Invoke(null, new object[] { context, batch });
    }

    private static void InsertFor<T>(DbContext context, List<object> batch)
        where T : class
    {
        var set = context.Set<T>();
        foreach (var entity in batch)
        {
            set.Add((T)entity);
        }
    }

    /// <summary>
    /// Determines whether a query exception is caused by schema drift between the
    /// current model and the source database (the source was created by an older
    /// version). Recognizes missing tables and missing columns for both SQLite and
    /// PostgreSQL, and writes a human-readable reason when matched.
    /// </summary>
    private static bool TryClassifySchemaDrift(Exception ex, IEntityType entityType, out string reason)
    {
        reason = string.Empty;
        var message = ex.Message ?? string.Empty;
        var table = entityType.GetTableName() ?? string.Empty;

        // SQLite: "SQLite Error 1: 'no such table: TableName'."
        if (message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            reason = "table not present in source";
            return true;
        }

        // SQLite: "SQLite Error 1: 'no such column: X'."
        if (message.Contains("no such column", StringComparison.OrdinalIgnoreCase))
        {
            reason = "source table is missing a column (older schema)";
            return true;
        }

        // PostgreSQL: 'relation "TableName" does not exist' or
        // 'undefined table: TableName'.
        if ((message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("undefined table", StringComparison.OrdinalIgnoreCase)) &&
            message.Contains(table, StringComparison.OrdinalIgnoreCase))
        {
            reason = "table not present in source";
            return true;
        }

        // PostgreSQL: 'column "X" does not exist'.
        if (message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("column", StringComparison.OrdinalIgnoreCase))
        {
            reason = "source table is missing a column (older schema)";
            return true;
        }

        return false;
    }
}
