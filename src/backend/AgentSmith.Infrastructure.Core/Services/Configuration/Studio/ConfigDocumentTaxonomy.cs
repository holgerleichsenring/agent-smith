using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0349: the complete ConfigEntityType &lt;-&gt; RawAgentSmithConfig-property map — the
/// store's core. Collections are keyed by catalog id; singletons carry the fixed
/// id 'default'. Adding a config type is exactly one entry here plus a C# record;
/// adding a field inside an existing type is zero db migration (the doc is opaque
/// JSON). Only projects carry reference edges.
/// </summary>
internal static class ConfigDocumentTaxonomy
{
    public static readonly IReadOnlyList<ConfigDocDescriptor> All =
    [
        ConfigDocDescriptor.Collection(ConfigDocTypes.Agent, r => r.Agents),
        ConfigDocDescriptor.Collection(ConfigDocTypes.Tracker, r => r.Trackers),
        ConfigDocDescriptor.Collection(ConfigDocTypes.Connection, r => r.Connections),
        ConfigDocDescriptor.Collection(ConfigDocTypes.Repo, r => r.Repos),
        ConfigDocDescriptor.Collection(ConfigDocTypes.Project, r => r.Projects, ProjectEdges),
        ConfigDocDescriptor.Collection(ConfigDocTypes.McpServer, r => r.McpServers),
        ConfigDocDescriptor.Collection(ConfigDocTypes.Secret, r => r.Secrets),
        ConfigDocDescriptor.Collection(ConfigDocTypes.PipelineTrigger, r => r.PipelineTriggers),

        ConfigDocDescriptor.Singleton(ConfigDocTypes.Registries, r => r.Registries, (r, v) => r.Registries = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.Queue, r => r.Queue, (r, v) => r.Queue = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.Skills, r => r.Skills, (r, v) => r.Skills = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.PrimaryProvider, r => new PrimaryProviderDoc(r.PrimaryProvider),
            (r, v) => r.PrimaryProvider = v.Value),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.Limits, r => r.Limits, (r, v) => r.Limits = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.PipelineStorage, r => r.PipelineStorage,
            (r, v) => r.PipelineStorage = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.PipelineDataFlow, r => r.PipelineDataFlow,
            (r, v) => r.PipelineDataFlow = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.Deployment, r => r.Deployment, (r, v) => r.Deployment = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.Sandbox, r => r.Sandbox, (r, v) => r.Sandbox = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.Orchestrator, r => r.Orchestrator, (r, v) => r.Orchestrator = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.Dialogue, r => r.Dialogue, (r, v) => r.Dialogue = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.Persistence, r => r.Persistence, (r, v) => r.Persistence = v),
        ConfigDocDescriptor.Singleton(ConfigDocTypes.PipelineCostCap, r => r.PipelineCostCap,
            (r, v) => r.PipelineCostCap = v),
    ];

    private static IEnumerable<ConfigDocEdge> ProjectEdges(JsonElement doc)
    {
        var project = doc.Deserialize<RawProjectEntry>(ConfigDocJson.Options);
        if (project is null) yield break;
        if (!string.IsNullOrWhiteSpace(project.Agent)) yield return new(ConfigDocTypes.Agent, project.Agent);
        if (!string.IsNullOrWhiteSpace(project.Tracker)) yield return new(ConfigDocTypes.Tracker, project.Tracker);
        foreach (var repoRef in project.Repos.Select(r => r.Ref))
        {
            var slash = repoRef.IndexOf('/');
            yield return slash > 0
                ? new ConfigDocEdge(ConfigDocTypes.Connection, repoRef[..slash])
                : new ConfigDocEdge(ConfigDocTypes.Repo, repoRef);
        }
    }
}

/// <summary>p0349: the single-scalar singleton doc for the root-level primary provider.</summary>
internal sealed record PrimaryProviderDoc(string? Value);
