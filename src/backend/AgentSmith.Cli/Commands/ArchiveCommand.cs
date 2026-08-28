using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Cli.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// 2026-08-28-2af6: `agentsmith archive export|import` — the whole run store into one zip
/// and back, so an installation can move between SQLite and SQL Server without losing a
/// row. It talks to the DATABASE the config file names, never to a running server: the
/// neighbouring case to a provider swap is an installation whose server will not start.
/// </summary>
internal static class ArchiveCommand
{
    private const string Confidentiality =
        "The archive holds ticket text, model prompts, artifacts and the configuration "
        + "store's secrets in clear. It is as confidential as the database itself.";

    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var target = new Argument<string>("archive", "The archive file (.zip).");
        var export = new Command("export", "Write every table of the store into one archive (DB -> zip).")
        {
            target, configOption, verboseOption,
        };
        export.SetHandler(async (InvocationContext ctx) => ctx.ExitCode = await ExportAsync(
            ctx.ParseResult.GetValueForOption(configOption)!,
            ctx.ParseResult.GetValueForOption(verboseOption),
            ctx.ParseResult.GetValueForArgument(target), ctx.GetCancellationToken()));

        var source = new Argument<string>("archive", "The archive file to read.");
        var import = new Command("import", "Write an archive into an EMPTY database (zip -> DB).")
        {
            source, configOption, verboseOption,
        };
        import.SetHandler(async (InvocationContext ctx) => ctx.ExitCode = await ImportAsync(
            ctx.ParseResult.GetValueForOption(configOption)!,
            ctx.ParseResult.GetValueForOption(verboseOption),
            ctx.ParseResult.GetValueForArgument(source), ctx.GetCancellationToken()));

        return new Command("archive", "Move a whole installation's data between providers.")
        {
            export, import,
        };
    }

    private static async Task<int> ExportAsync(
        string configPath, bool verbose, string archivePath, CancellationToken ct)
    {
        await using var services = ServiceProviderFactory.Build(configPath, verbose, headless: true);
        await using var db = services.GetRequiredService<ConfiguredStoreFactory>().Create(configPath);
        try
        {
            await using var file = File.Create(archivePath);
            var manifest = await services.GetRequiredService<IDataArchiveWriter>()
                .WriteAsync(db, file, ct);
            Console.WriteLine(
                $"Wrote {manifest.Tables.Sum(t => t.Rows)} rows from {manifest.Tables.Count} tables "
                + $"at schema '{manifest.SchemaHead}' to {archivePath}.");
            Console.WriteLine(Confidentiality);
            return 0;
        }
        catch (DataArchiveException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<int> ImportAsync(
        string configPath, bool verbose, string archivePath, CancellationToken ct)
    {
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Archive not found: {archivePath}");
            return 1;
        }

        await using var services = ServiceProviderFactory.Build(configPath, verbose, headless: true);
        await using var db = services.GetRequiredService<ConfiguredStoreFactory>().Create(configPath);
        try
        {
            await using var file = File.OpenRead(archivePath);
            var report = await services.GetRequiredService<IDataArchiveReader>().ReadAsync(db, file, ct);
            Console.WriteLine(
                $"Imported {report.TotalRows} rows into {report.Written.Count} tables, verified "
                + $"against the manifest taken at schema '{report.Manifest.SchemaHead}'.");
            return 0;
        }
        catch (DataArchiveException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
