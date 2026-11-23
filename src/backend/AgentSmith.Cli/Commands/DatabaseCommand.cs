using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Infrastructure.Migration.Extensions;
using AgentSmith.Infrastructure.Migration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// `agentsmith database migrate` — applies pending relational-persistence
/// migrations EXPLICITLY, the way a deployment pipeline does it (idempotent,
/// before the server starts). Migrating on app startup is bad practice (races
/// between replicas, surprises operators), so the server never does it; this is
/// the single, deliberate entry point. Runs programmatically via
/// Database.MigrateAsync — no `dotnet ef` / .NET SDK needed at the run site.
/// 2026-11-22-747da: the body is <see cref="StoreMigrateRun"/>, the same one the server image runs.
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
        var services = new ServiceCollection().AddLogging(builder =>
        {
            builder.AddConsoleFormatter<ShortCategoryFormatter, ShortCategoryFormatterOptions>();
            builder.AddConsole(options => options.FormatterName = "short");
            builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
            if (!verbose) builder.AddFilter(ServiceProviderFactory.EfCoreCategory, LogLevel.Warning);
        });
        await using var provider = services.AddStoreMigrate(configPath).BuildServiceProvider();
        return await provider.GetRequiredService<StoreMigrateRun>().RunAsync(ct);
    }
}
