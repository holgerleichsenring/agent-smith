using AgentSmith.Cli;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Services;

/// <summary>
/// `agentsmith config import`: plans the YAML into store documents and writes them, guarded.
/// The planner and the store both refuse with <see cref="ConfigurationException"/>, so one
/// boundary covers both — a refused plan reads exactly like a refused write: its message on
/// stderr and exit code 1, never a stack trace.
/// </summary>
internal sealed class ConfigImportRunner(ConfigStoreContextFactory contexts)
{
    public async Task<int> RunAsync(string configPath, bool verbose, string yamlPath, bool force)
    {
        if (!File.Exists(yamlPath))
        {
            Console.Error.WriteLine($"Import file not found: {yamlPath}");
            return 1;
        }
        try
        {
            return await ImportAsync(configPath, verbose, yamlPath, force);
        }
        catch (ConfigurationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private async Task<int> ImportAsync(string configPath, bool verbose, string yamlPath, bool force)
    {
        using var services = ServiceProviderFactory.Build(configPath, verbose, headless: true);
        // The planner leaves out persistence (bootstrap-only) and names every key it drops.
        var plan = services.GetRequiredService<ConfigImportPlanner>()
            .Plan(await File.ReadAllTextAsync(yamlPath), yamlPath);
        foreach (var dropped in plan.Dropped)
            Console.Error.WriteLine($"Not imported: {dropped.Path} — {dropped.Reason}");
        var writes = plan.Docs.Select(ToWrite).ToList();
        await using var db = contexts.Create(configPath, verbose);
        new ConfigImportRepository(db).Import(writes, force);
        Console.WriteLine($"Imported {writes.Count} config entities from {yamlPath}.");
        return 0;
    }

    private static ConfigDocWrite ToWrite(DecomposedConfigDoc doc) =>
        new(doc.Type, doc.Id, doc.Doc, ExpectedVersion: null, doc.Edges, ChangedBy: "cli-import");
}
