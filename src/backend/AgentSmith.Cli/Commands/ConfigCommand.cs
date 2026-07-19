using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// p0349: `agentsmith config export|import` — the guarded DR + cutover path between
/// the server's DB entity-document store and a YAML file. NOT an auto-seed: import
/// is deliberate and guarded (empty store, or --force). Touches the DB the same
/// deliberate way `database migrate` does; the scan pipeline stays file-only.
/// </summary>
internal static class ConfigCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var output = new Option<string?>("--output", "Write the exported YAML here (default: stdout).");
        var export = new Command("export", "Export the DB config store to agentsmith.yml (DB -> YAML).")
        {
            configOption, verboseOption, output,
        };
        export.SetHandler(async (InvocationContext ctx) => ctx.ExitCode = await ExportAsync(
            ctx.ParseResult.GetValueForOption(configOption)!,
            ctx.ParseResult.GetValueForOption(verboseOption),
            ctx.ParseResult.GetValueForOption(output)));

        var file = new Argument<string>("yaml", "The agentsmith.yml to import.");
        var force = new Option<bool>("--force", "Overwrite a non-empty store (versions are bumped, history kept).");
        var import = new Command("import", "Import a YAML config into the DB store (YAML -> DB), guarded.")
        {
            file, configOption, verboseOption, force,
        };
        import.SetHandler(async (InvocationContext ctx) => ctx.ExitCode = await ImportAsync(
            ctx.ParseResult.GetValueForOption(configOption)!,
            ctx.ParseResult.GetValueForOption(verboseOption),
            ctx.ParseResult.GetValueForArgument(file),
            ctx.ParseResult.GetValueForOption(force)));

        return new Command("config", "Config store DR + cutover (server mode).") { export, import };
    }

    private static async Task<int> ExportAsync(string configPath, bool verbose, string? output)
    {
        await using var db = BuildContext(configPath, verbose);
        var raw = new ConfigDocumentAssembler().Assemble(new ConfigDocumentRepository(db).LoadAll());
        var yaml = RawConfigYaml.Serialize(raw);
        if (string.IsNullOrEmpty(output)) Console.WriteLine(yaml);
        else await File.WriteAllTextAsync(output, yaml);
        return 0;
    }

    private static async Task<int> ImportAsync(string configPath, bool verbose, string yamlPath, bool force)
    {
        if (!File.Exists(yamlPath))
        {
            Console.Error.WriteLine($"Import file not found: {yamlPath}");
            return 1;
        }
        var raw = RawConfigYaml.Deserialize(await File.ReadAllTextAsync(yamlPath));
        // persistence is bootstrap-only (read from the file/env before the DB), so it is
        // never imported into the DB it describes — the same exclusion the UI import applies.
        var writes = new ConfigDocumentAssembler().Decompose(raw)
            .Where(d => d.Type != ConfigDocTypes.Persistence)
            .Select(ToWrite).ToList();
        await using var db = BuildContext(configPath, verbose);
        try
        {
            new ConfigImportRepository(db).Import(writes, force);
            Console.WriteLine($"Imported {writes.Count} config entities from {yamlPath}.");
            return 0;
        }
        catch (ConfigurationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static ConfigDocWrite ToWrite(DecomposedConfigDoc doc) =>
        new(doc.Type, doc.Id, doc.Doc, ExpectedVersion: null, doc.Edges, ChangedBy: "cli-import");

    private static AgentSmithDbContext BuildContext(string configPath, bool verbose)
    {
        var services = ServiceProviderFactory.Build(verbose, headless: true, configPath: configPath);
        var persistence = services.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath).Persistence;
        var provider = Enum.TryParse<PersistenceProvider>(persistence.Provider, ignoreCase: true, out var p)
            ? p : PersistenceProvider.Sqlite;
        var options = new PersistenceOptions { Provider = provider, ConnectionString = persistence.ConnectionString };
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(options);
        if (provider != PersistenceProvider.Sqlite)
            builder.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        return new AgentSmithDbContext(builder.Options);
    }
}
