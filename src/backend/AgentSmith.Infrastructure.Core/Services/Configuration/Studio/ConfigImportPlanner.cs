using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Infrastructure.Core.Services.Configuration.Retired;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// Turns an agentsmith.yml into the documents an import stores, and names every key of the
/// file the store will not keep — a retired key, a bootstrap key the server reads from its
/// own file, or a key no setting has. An import used to drop them without a word, so a
/// misspelt key looked imported and did nothing.
/// </summary>
public sealed class ConfigImportPlanner(
    RawConfigYaml yaml, ConfigDocumentAssembler assembler, RawConfigTreeReader trees,
    StoredKeyDiff diff, RetiredConfigKeyDetector retired)
{
    /// <summary>Read from the server's own file at boot, never stored in the database.</summary>
    private static readonly HashSet<string> BootstrapRoots =
        new(StringComparer.OrdinalIgnoreCase) { "persistence", "auth", "tool_runner" };

    public ConfigImportPlan Plan(string text, string source)
    {
        var raw = yaml.Deserialize(text);
        CatalogUseRule.Validate(raw.Agents);
        var docs = assembler.Decompose(raw)
            .Where(d => d.Type != ConfigDocTypes.Persistence)
            .ToList();
        var stored = trees.FromStoredDocuments([.. docs.Select(d => new ConfigDocRow(d.Type, d.Id, d.Doc, 0))]);
        var retiredReasons = retired.InYaml(text, source)
            .Where(f => f.Field is not null)
            .GroupBy(f => Folded(f.Field!))
            .ToDictionary(g => g.Key, g => g.First().Reason);
        var dropped = diff.Missing(trees.FromYaml(text), stored)
            .Select(path => new DroppedConfigKey(path, Reason(path, retiredReasons)))
            .ToList();
        return new ConfigImportPlan(docs, dropped);
    }

    private static string Reason(string path, IReadOnlyDictionary<string, string> retiredReasons) =>
        retiredReasons.TryGetValue(Folded(path), out var reason) ? reason
        : BootstrapRoots.Contains(path.Split('.')[0])
            ? "read from the server's own configuration file at boot and never stored; keep it in that file"
            : "no setting has this name, so nothing reads it — check the spelling against agentsmith.schema.json";

    private static string Folded(string path) => path.Replace("_", "").ToLowerInvariant();
}
