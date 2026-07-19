using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Interceptors;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// `agentsmith database migrate` — applies pending relational-persistence
/// migrations EXPLICITLY, the way a deployment pipeline does it (idempotent,
/// before the server starts). Migrating on app startup is bad practice (races
/// between replicas, surprises operators), so the server never does it; this is
/// the single, deliberate entry point. Runs programmatically via
/// Database.MigrateAsync — no `dotnet ef` / .NET SDK needed at the run site.
/// </summary>
internal static class DatabaseCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var migrate = new Command("migrate",
            "Apply pending relational-persistence migrations (run in the pipeline before the server; never on startup).")
        {
            configOption, verboseOption,
        };

        migrate.SetHandler(async (InvocationContext ctx) =>
        {
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            ctx.ExitCode = await RunAsync(configPath, verbose, ctx.GetCancellationToken());
        });

        return new Command("database", "Relational persistence maintenance (server mode).") { migrate };
    }

    private static async Task<int> RunAsync(string configPath, bool verbose, CancellationToken ct)
    {
        await using var services = ServiceProviderFactory.Build(verbose, headless: true, configPath: configPath);
        var persistence = services.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath).Persistence;

        if (!Enum.TryParse<PersistenceProvider>(persistence.Provider, ignoreCase: true, out var provider))
        {
            Console.Error.WriteLine(
                $"Unknown persistence.provider '{persistence.Provider}' (expected sqlite | postgresql | mysql).");
            return 1;
        }

        var options = new PersistenceOptions { Provider = provider, ConnectionString = persistence.ConnectionString };
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(options);
        await using var db = new AgentSmithDbContext(builder.Options);

        // p0348: leave the SQLite file in WAL before the server ever opens it.
        // Without this the migrate one-shot leaves the file in DELETE journal mode
        // and the first server connection must perform the WAL conversion under
        // boot contention — the intermittent "database is locked" at startup. WAL
        // persists in the file header, so applying it here carries over to the
        // server. Runs on the context's own connection so the mode is set before
        // MigrateAsync opens its transactions.
        if (provider == PersistenceProvider.Sqlite)
        {
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(ct);
            await using var pragma = connection.CreateCommand();
            pragma.CommandText = SqliteTuningInterceptor.TunePragmas;
            await pragma.ExecuteNonQueryAsync(ct);
        }

        // Don't log the connection string — it may carry Postgres/MySQL credentials.
        Console.WriteLine($"Applying relational-persistence migrations (provider={persistence.Provider})...");
        await db.Database.MigrateAsync(ct);
        Console.WriteLine("Done — schema is up to date.");
        return 0;
    }
}
