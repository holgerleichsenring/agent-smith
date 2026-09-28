using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Tests.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// Every block the loader binds is a stored document, or says here why not. A block with no
/// descriptor is dropped by an import and missing from a server that reads the store — trace
/// was, so a server could not switch tracing on from its own configuration.
/// </summary>
public sealed class ConfigTaxonomyCoverageTests
{
    private static readonly IReadOnlyDictionary<string, string> CollectionTypes = new Dictionary<string, string>
    {
        ["agents"] = ConfigDocTypes.Agent, ["trackers"] = ConfigDocTypes.Tracker,
        ["connections"] = ConfigDocTypes.Connection, ["repos"] = ConfigDocTypes.Repo,
        ["projects"] = ConfigDocTypes.Project, ["mcp_servers"] = ConfigDocTypes.McpServer,
        ["secrets"] = ConfigDocTypes.Secret, ["pipeline_triggers"] = ConfigDocTypes.PipelineTrigger,
    };

    private static readonly IReadOnlyDictionary<string, string> NotStored = new Dictionary<string, string>
    {
        ["auth"] = "bootstrap: the token authority is registered before the database exists, "
                   + "so it is read from the file and the environment and never stored",
    };

    [Fact]
    public void Taxonomy_CoversEveryRawRootProperty_OrNamesWhyNot()
    {
        var stored = ConfigDocumentTaxonomy.All.Select(d => d.Type).ToHashSet(StringComparer.Ordinal);

        ConfigSchemaCoverage.BoundKeys(typeof(RawAgentSmithConfig)).Keys
            .Where(key => !NotStored.ContainsKey(key))
            .Where(key => !stored.Contains(CollectionTypes.GetValueOrDefault(key, key)))
            .Should().BeEmpty("a bound block without a stored document is dropped by every import");
    }
}
