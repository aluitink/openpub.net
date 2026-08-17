using ActivityPub.Core.Migration;
using ActivityPub.Core.Options;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ActivityPub.Tests.Migration;

public class DatabaseOptionsTests
{
    [Fact]
    public void Defaults_UseSqliteProvider()
    {
        var options = new DatabaseOptions();
        Assert.Equal(DatabaseProvider.Sqlite, options.Provider);
        Assert.Equal(".", options.DataDirectory);
        Assert.Equal("fediblog.db", options.IdentityDatabaseFile);
        Assert.Equal("fediblog_ap.db", options.FederationDatabaseFile);
        Assert.Null(options.IdentityConnection);
        Assert.Null(options.FederationConnection);
    }

    [Fact]
    public void Sqlite_IdentityConnection_ResolvesToDataDirectoryPath()
    {
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.Sqlite,
            DataDirectory = "/var/data"
        };
        var cs = options.GetIdentityConnectionString();
        Assert.Contains("fediblog.db", cs);
        Assert.Contains("Data Source=", cs);
        Assert.Contains("/var/data", cs);
    }

    [Fact]
    public void Sqlite_FederationConnection_ResolvesToDataDirectoryPath()
    {
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.Sqlite,
            DataDirectory = "/var/data"
        };
        var cs = options.GetFederationConnectionString();
        Assert.Contains("fediblog_ap.db", cs);
        Assert.Contains("Data Source=", cs);
    }

    [Fact]
    public void Postgresql_IdentityConnection_UsesConfiguredString()
    {
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.Postgresql,
            IdentityConnection = "Host=db.example.com;Database=id;Username=ap;Password=secret"
        };
        Assert.Equal("Host=db.example.com;Database=id;Username=ap;Password=secret",
            options.GetIdentityConnectionString());
    }

    [Fact]
    public void Postgresql_WithoutExplicitConnection_FallsBackToDefaultLocal()
    {
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.Postgresql
        };
        var cs = options.GetFederationConnectionString();
        Assert.StartsWith("Host=localhost", cs);
        Assert.Contains("fediblog_ap", cs);
    }

    [Fact]
    public void Sqlite_ExplicitConnectionString_WinsOverDataDirectory()
    {
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.Sqlite,
            DataDirectory = "/ignored",
            FederationConnection = "Data Source=/explicit/path.db"
        };
        Assert.Equal("Data Source=/explicit/path.db", options.GetFederationConnectionString());
    }

    [Fact]
    public void ActivityPubOptions_ExposesDatabaseSection()
    {
        var options = new ActivityPubOptions();
        Assert.NotNull(options.Database);
        Assert.Equal(DatabaseProvider.Sqlite, options.Database.Provider);
    }
}

public class ProviderConditionalModelTests
{
    /// <summary>
    /// Reads the configured default SQL for a value-generated DateTime property on
    /// the <see cref="ActorEntity"/>.SQLite must use <c>datetime('now')</c> and
    /// PostgreSQL must use <c>now()</c>.
    /// </summary>
    private static string GetCreatedAtDefaultSql(DatabaseProvider provider)
    {
        string connectionString = provider == DatabaseProvider.Postgresql
            ? "Host=localhost;Database=model_check;Username=ap;Password=ap"
            : "Data Source=/tmp/model_check.db";

        using var context = DatabaseMigrationService.CreateContext<ActivityPubDbContext>(
            provider, connectionString);
        // Force model building without touching the database.
        var entityType = context.Model.FindEntityType(typeof(ActorEntity))!;
        var property = entityType.FindProperty(nameof(ActorEntity.CreatedAt))!;
        return property.GetDefaultValueSql() ?? string.Empty;
    }

    [Fact]
    public void Sqlite_Model_UsesDatetimeNow()
    {
        Assert.Equal("datetime('now')", GetCreatedAtDefaultSql(DatabaseProvider.Sqlite));
    }

    [Fact]
    public void Postgresql_Model_UsesNow()
    {
        Assert.Equal("now()", GetCreatedAtDefaultSql(DatabaseProvider.Postgresql));
    }
}

public class DatabaseMigrationServiceTests : IDisposable
{
    private readonly string _sourceDb;
    private readonly string _targetDb;

    public DatabaseMigrationServiceTests()
    {
        var stamp = Guid.NewGuid().ToString("N");
        _sourceDb = $"/tmp/migrate_src_{stamp}.db";
        _targetDb = $"/tmp/migrate_tgt_{stamp}.db";
    }

    public void Dispose()
    {
        TryDelete(_sourceDb);
        TryDelete(_targetDb);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; ignore failures.
        }
    }

    private static ActivityPubDbContext CreateSqliteContext(string path)
    {
        var options = new DbContextOptionsBuilder<ActivityPubDbContext>()
            .UseSqlite($"Data Source={path}").Options;
        return new ActivityPubDbContext(options);
    }

    [Fact]
    public async Task Migrate_CopiesActorsAndActivities_FromSqliteToSqlite()
    {
        // Seed the source with a few actors and activities.
        await using (var source = CreateSqliteContext(_sourceDb))
        {
            await source.Database.EnsureCreatedAsync();
            source.Actors.AddRange(
                new ActorEntity { Id = 1, Username = "alice", JsonData = "{\"type\":\"Person\"}" },
                new ActorEntity { Id = 2, Username = "bob", JsonData = "{\"type\":\"Person\"}" },
                new ActorEntity { Id = 3, Username = "carol", JsonData = "{\"type\":\"Person\"}" });
            source.Activities.AddRange(
                new ActivityEntity { Id = 10, ActivityId = "urn:activity:1", JsonData = "{\"type\":\"Note\"}" },
                new ActivityEntity { Id = 11, ActivityId = "urn:activity:2", JsonData = "{\"type\":\"Note\"}" });
            await source.SaveChangesAsync();
        }

        // Ensure the target exists (empty).
        await using (var targetInit = CreateSqliteContext(_targetDb))
        {
            await targetInit.Database.EnsureCreatedAsync();
        }

        var progress = new List<MigrationProgress>();
        var progressReporter = new Progress<MigrationProgress>(p => progress.Add(p));

        var service = new DatabaseMigrationService { BatchSize = 2 };
        var result = await service.MigrateFederationAsync(
            DatabaseProvider.Sqlite, $"Data Source={_sourceDb}",
            DatabaseProvider.Sqlite, $"Data Source={_targetDb}",
            progress: progressReporter,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.TotalRowsCopied >= 5, $"Expected >= 5 rows, got {result.TotalRowsCopied}");

        // Verify the target now contains the copied rows with preserved keys.
        await using (var target = CreateSqliteContext(_targetDb))
        {
            Assert.Equal(3, await target.Actors.CountAsync());
            Assert.Equal(2, await target.Activities.CountAsync());

            // Primary keys are preserved (original IDs reused).
            var alice = await target.Actors.FirstAsync(a => a.Id == 1);
            Assert.Equal("alice", alice.Username);

            var act = await target.Activities.FirstAsync(a => a.Id == 10);
            Assert.Equal("urn:activity:1", act.ActivityId);
        }

        // Progress reports were emitted for the migrated entities.
        Assert.NotEmpty(progress);
        Assert.Contains(progress, p => p.EntityType == "ActorEntity" && p.IsComplete);
    }

    [Fact]
    public async Task Migrate_ReportsZeroRowsForEmptySource()
    {
        await using (var source = CreateSqliteContext(_sourceDb))
        {
            await source.Database.EnsureCreatedAsync();
        }

        await using (var targetInit = CreateSqliteContext(_targetDb))
        {
            await targetInit.Database.EnsureCreatedAsync();
        }

        var service = new DatabaseMigrationService { BatchSize = 100 };
        var result = await service.MigrateFederationAsync(
            DatabaseProvider.Sqlite, $"Data Source={_sourceDb}",
            DatabaseProvider.Sqlite, $"Data Source={_targetDb}",
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalRowsCopied);
        // Every entity reports zero rows copied (none skipped).
        Assert.All(result.Entities, e => Assert.Equal(0, e.RowsCopied));
    }

    [Fact]
    public async Task Migrate_OrdersEntitiesByForeignKeyDependencies()
    {
        await using var context = CreateSqliteContext(_sourceDb);
        await context.Database.EnsureCreatedAsync();

        // OrderEntityTypes must place an entity after the entities it references.
        // OAuth2AccessTokenEntity has a foreign key to ActorEntity (ActorId ->
        // Actor), so ActorEntity must be ordered first.
        var order = DatabaseMigrationService.OrderEntityTypes(context.Model).ToList();
        var names = order.Select(e => e.ShortName()).ToList();
        Assert.Contains("ActorEntity", names);
        Assert.Contains("OAuth2AccessTokenEntity", names);
        Assert.True(
            names.IndexOf("ActorEntity") < names.IndexOf("OAuth2AccessTokenEntity"),
            "ActorEntity must be ordered before OAuth2AccessTokenEntity");
    }

    [Fact]
    public void CreateContext_UsesRequestedProvider()
    {
        using var sqliteCtx = DatabaseMigrationService.CreateContext<ActivityPubDbContext>(
            DatabaseProvider.Sqlite, "Data Source=/tmp/prov_check.db");
        Assert.False(sqliteCtx.Database.IsNpgsql());

        using var pgCtx = DatabaseMigrationService.CreateContext<ActivityPubDbContext>(
            DatabaseProvider.Postgresql, "Host=localhost;Database=prov_check;Username=ap;Password=ap");
        Assert.True(pgCtx.Database.IsNpgsql());
    }

    /// <summary>
    /// When the source database was created by an older schema that is missing a
    /// table the current model defines (e.g. <c>FederationPeers</c>, added in a
    /// later release), the migration must skip that entity, still copy the
    /// remaining data, and report success.
    /// </summary>
    [Fact]
    public async Task Migrate_SkipsEntitiesWhoseTableIsMissingInSource()
    {
        // Build the source with the CURRENT schema, then drop a table that a
        // brand-new install would have, simulating an older source database.
        await using (var source = CreateSqliteContext(_sourceDb))
        {
            await source.Database.EnsureCreatedAsync();
            source.Actors.Add(new ActorEntity { Id = 1, Username = "alice", JsonData = "{\"type\":\"Person\"}" });
            await source.SaveChangesAsync();

            // Drop the FederationPeers table to simulate schema drift.
            await source.Database
                .ExecuteSqlRawAsync("DROP TABLE \"FederationPeers\"");
        }

        await using (var targetInit = CreateSqliteContext(_targetDb))
        {
            await targetInit.Database.EnsureCreatedAsync();
        }

        var service = new DatabaseMigrationService { BatchSize = 100 };
        var result = await service.MigrateFederationAsync(
            DatabaseProvider.Sqlite, $"Data Source={_sourceDb}",
            DatabaseProvider.Sqlite, $"Data Source={_targetDb}",
            progress: null,
            cancellationToken: CancellationToken.None);

        // The migration as a whole succeeds despite the drift.
        Assert.True(result.Success);

        // The drifted entity is reported as skipped with a reason.
        var skipped = result.Entities.FirstOrDefault(e => e.EntityType == "FederationPeerEntity");
        Assert.NotNull(skipped);
        Assert.True(skipped!.Skipped);
        Assert.Equal(0, skipped.RowsCopied);
        Assert.False(string.IsNullOrEmpty(skipped.Reason));

        // Entities whose tables are present still get copied.
        var actorResult = result.Entities.FirstOrDefault(e => e.EntityType == "ActorEntity");
        Assert.NotNull(actorResult);
        Assert.False(actorResult!.Skipped);
        Assert.Equal(1, actorResult.RowsCopied);

        // The target received the actor row and, thanks to EnsureCreated, has the
        // full current schema (including the FederationPeers table). Querying the
        // FederationPeers DbSet would throw if the table were missing.
        await using (var target = CreateSqliteContext(_targetDb))
        {
            Assert.Equal(1, await target.Actors.CountAsync());
            Assert.Equal(0, await target.FederationPeers.CountAsync());
        }
    }
}
