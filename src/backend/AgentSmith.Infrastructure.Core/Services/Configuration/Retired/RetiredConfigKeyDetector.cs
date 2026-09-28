using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Retired;

/// <summary>
/// Reports every <see cref="RetiredConfigKeys"/> row a configuration still sets, as an ADVISORY
/// finding: the key is ignored, which changes nothing a run does, and a blocking finding would
/// disable triggers over a key that already does nothing. The finding says how to clear it,
/// which differs between a file and the store.
/// </summary>
public sealed class RetiredConfigKeyDetector(RawConfigTreeReader reader, ConfigKeyPathMatcher matcher)
{
    /// <summary>The retired keys a configuration file sets.</summary>
    public IReadOnlyList<StartupFinding> InYaml(string yaml, string configPath) =>
        Find(reader.FromYaml(yaml), $"The configuration file '{configPath}'", "Delete it from the file.");

    /// <summary>The retired keys the stored configuration documents set.</summary>
    public IReadOnlyList<StartupFinding> InStoredDocuments(IReadOnlyList<ConfigDocRow> rows) =>
        Find(reader.FromStoredDocuments(rows), "The stored configuration",
            "Saving that entity once in the configuration studio drops it; so does exporting, "
            + "editing and re-importing the configuration.");

    private IReadOnlyList<StartupFinding> Find(JsonNode? tree, string where, string howToClear) =>
    [
        .. RetiredConfigKeys.All.SelectMany(key => matcher.SetPaths(tree, key.Path)
            .Select(path => new StartupFinding(
                StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
                $"{where} sets '{path}', which is not read since {key.Since} and is ignored: "
                + $"{key.Reason} {howToClear}",
                Field: path))),
    ];
}
