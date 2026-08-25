using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0515: assembles the public <see cref="AgentSmithConfig"/> from the raw shape and the
/// already-built catalogs. Extracted from <see cref="ConfigCatalogResolver"/>, whose reason
/// to change is how a reference RESOLVES — which keys the result object carries is a
/// different one, and it changes every time a configuration block is added.
/// </summary>
public sealed class ResolvedConfigComposer
{
    public AgentSmithConfig Compose(
        RawAgentSmithConfig raw,
        ConfigCatalogs catalogs,
        Dictionary<string, ResolvedProject> projects) =>
        new()
        {
            Agents = catalogs.Agents,
            Repos = catalogs.Repos,
            Connections = catalogs.Connections,
            Trackers = catalogs.Trackers,
            PipelineTriggers = new PipelineTriggerMap(raw.PipelineTriggers),
            Projects = projects,
            Secrets = raw.Secrets,
            Registries = BuildRegistries(raw),
            Queue = raw.Queue,
            Skills = raw.Skills,
            PrimaryProvider = raw.PrimaryProvider,
            Limits = raw.Limits,
            PipelineStorage = raw.PipelineStorage,
            PipelineDataFlow = raw.PipelineDataFlow,
            Sandbox = raw.Sandbox,
            Orchestrator = raw.Orchestrator,
            Dialogue = raw.Dialogue, // p0327
            Persistence = raw.Persistence, Trace = raw.Trace, // p0423
            PipelineCostCap = raw.PipelineCostCap,
        };

    private static IReadOnlyList<RegistryConfig> BuildRegistries(RawAgentSmithConfig raw)
    {
        if (raw.Registries.Count == 0) return Array.Empty<RegistryConfig>();
        var resolved = new List<RegistryConfig>(raw.Registries.Count);
        foreach (var entry in raw.Registries)
            resolved.Add(new RegistryConfig(entry.Host, entry.Username, entry.Token));
        return resolved;
    }
}
