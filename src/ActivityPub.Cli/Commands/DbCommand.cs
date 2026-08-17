using System.CommandLine;
using System.CommandLine.Invocation;
using ActivityPub.Core.Migration;
using ActivityPub.Core.Options;

namespace ActivityPub.Cli.Commands;

/// <summary>
/// The <c>db</c> command group. Provides <c>db migrate</c>, which copies the
/// ActivityPub federation data from a source database (typically the historical
/// SQLite file) to a target database (typically PostgreSQL).
///
/// The Identity database (users and roles) lives in the WebUI project's
/// <c>ApplicationDbContext</c>; migrating it is performed by the WebUI host (see
/// <c>ActivityPub.WebUI.Program</c>) because the CLI does not reference WebUI.
/// </summary>
public static class DbCommand
{
    public static Command Create()
    {
        var dbCommand = new Command("db", "Manage the relational database");

        var migrateCommand = new Command(
            "migrate",
            "Copy ActivityPub federation data from a source to a target database (e.g. SQLite -> PostgreSQL)");

        var sourceProvider = new Option<DatabaseProvider>(
            "--source-provider",
            description: "Source database provider (Sqlite or Postgresql). Defaults to Sqlite.",
            getDefaultValue: () => DatabaseProvider.Sqlite);

        var sourceConnection = new Option<string>(
            "--source-connection",
            description: "Source federation connection string (file path for SQLite). Defaults to the current directory's fediblog_ap.db.");

        var targetProvider = new Option<DatabaseProvider>(
            "--target-provider",
            description: "Target database provider (Sqlite or Postgresql). Defaults to Postgresql.",
            getDefaultValue: () => DatabaseProvider.Postgresql);

        var targetConnection = new Option<string>(
            "--target-connection",
            description: "Target federation connection string. Defaults to a local PostgreSQL fediblog_ap database.");

        var batchSize = new Option<int>(
            "--batch-size",
            description: "Rows per save during the copy. Defaults to 500.",
            getDefaultValue: () => 500);

        migrateCommand.AddOption(sourceProvider);
        migrateCommand.AddOption(sourceConnection);
        migrateCommand.AddOption(targetProvider);
        migrateCommand.AddOption(targetConnection);
        migrateCommand.AddOption(batchSize);

        migrateCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var sp = ctx.ParseResult.GetValueForOption(sourceProvider);
            var sc = ctx.ParseResult.GetValueForOption(sourceConnection) ?? "Data Source=fediblog_ap.db";
            var tp = ctx.ParseResult.GetValueForOption(targetProvider);
            var tc = ctx.ParseResult.GetValueForOption(targetConnection)
                     ?? "Host=localhost;Database=fediblog_ap;Username=ap;Password=ap";
            var bs = ctx.ParseResult.GetValueForOption(batchSize);

            var service = new DatabaseMigrationService { BatchSize = bs };

            Console.WriteLine($"Migrating federation database: {sp} -> {tp}");
            Console.WriteLine($"  Source: {sc}");
            Console.WriteLine($"  Target: {tc}");

            var result = await service.MigrateFederationAsync(
                sp, sc, tp, tc,
                progress: null,
                cancellationToken: ctx.GetCancellationToken());

            PrintResult(result);

            Console.WriteLine();
            Console.WriteLine(result.Success ? "Migration complete." : "Migration finished with errors.");
            ctx.ExitCode = result.Success ? 0 : 1;
        });

        dbCommand.AddCommand(migrateCommand);
        return dbCommand;
    }

    private static void PrintResult(ContextMigrationResult result)
    {
        foreach (var entity in result.Entities)
        {
            var status = entity.Skipped ? "SKIPPED" : $"copied {entity.RowsCopied} rows";
            var reason = entity.Reason != null ? $" ({entity.Reason})" : "";
            Console.WriteLine($"  - {entity.EntityType}: {status}{reason}");
        }
        Console.WriteLine($"  Total: {result.TotalRowsCopied} rows across {result.Entities.Count} entities");
    }
}
