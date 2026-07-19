using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// Projects a full-fidelity <see cref="RawAgentSmithConfig"/> (the YAML-bound
/// document FileConfigStore keeps as the source of truth) onto the thin,
/// editable <see cref="ConfigCatalog"/> the studio and API operate on. The
/// reverse direction is deliberately a PATCH on the raw document (see
/// FileConfigStore) so global blocks, triggers and per-role model routing the
/// studio does not surface survive an export round-trip untouched.
/// </summary>
internal static class ConfigCatalogMapper
{
    public static ConfigCatalog ToCatalog(RawAgentSmithConfig raw) =>
        new(
            Agents: raw.Agents.Select(kv => ToAgent(kv.Key, kv.Value)).ToList(),
            Trackers: raw.Trackers.Select(kv => ToTracker(kv.Key, kv.Value)).ToList(),
            Repos: raw.Repos.Select(kv => ToRepo(kv.Key, kv.Value)).ToList(),
            Projects: raw.Projects.Select(kv => ToProject(kv.Key, kv.Value)).ToList(),
            McpServers: raw.McpServers.Select(kv => ToMcpServer(kv.Key, kv.Value)).ToList(),
            Secrets: raw.Secrets.Keys.Select(k => new SecretEntity(k)).ToList(),
            Connections: raw.Connections.Select(kv => ToConnection(kv.Key, kv.Value)).ToList());

    // p0345c: the FULL raw agent surface. Model routing surfaces the EFFECTIVE
    // registry (defaults filled by binding are shown as-is — what runs is what
    // the operator sees); the reserved "coding" role carries the top-level
    // model/deployment pair. Sections not surfaced here (parallelism, rate
    // limit, loop tuning) survive upsert untouched via the patch builders.
    private static AgentEntity ToAgent(string id, AgentConfig agent)
    {
        var models = new Dictionary<string, AgentModelAssignment>
        {
            ["coding"] = new(agent.Model, agent.Deployment),
        };
        if (agent.Models is { } registry)
        {
            models["scout"] = ToAssignment(registry.Scout);
            models["primary"] = ToAssignment(registry.Primary);
            models["planning"] = ToAssignment(registry.Planning);
            if (registry.Reasoning is { } reasoning) models["reasoning"] = ToAssignment(reasoning);
            models["summarization"] = ToAssignment(registry.Summarization);
            models["contextGeneration"] = ToAssignment(registry.ContextGeneration);
            models["codeMapGeneration"] = ToAssignment(registry.CodeMapGeneration);
        }
        return new AgentEntity(
            id,
            agent.Type,
            agent.ApiKeySecret,
            agent.Endpoint,
            agent.ApiVersion,
            agent.NetworkTimeoutSeconds,
            models,
            agent.Pricing.Models.Count > 0
                ? new AgentPricing(agent.Pricing.Models.ToDictionary(
                    kv => kv.Key,
                    kv => new AgentModelPricing(
                        kv.Value.InputPerMillion,
                        kv.Value.OutputPerMillion,
                        kv.Value.CacheReadPerMillion)))
                : null,
            new AgentCacheSettings(agent.Cache.IsEnabled, agent.Cache.Strategy),
            new AgentCompactionSettings(
                agent.Compaction.IsEnabled,
                agent.Compaction.ThresholdIterations,
                agent.Compaction.MaxContextTokens,
                agent.Compaction.KeepRecentIterations,
                agent.Compaction.SummaryModel),
            new AgentRetrySettings(
                agent.Retry.MaxRetries,
                agent.Retry.InitialDelayMs,
                agent.Retry.BackoffMultiplier,
                agent.Retry.MaxDelayMs));
    }

    private static AgentModelAssignment ToAssignment(ModelAssignment assignment) =>
        new(assignment.Model, assignment.Deployment, assignment.MaxTokens);

    // p0345c: full tracker surface — identity + tracker-owned workflow + polling.
    // Empty raw collections surface as null ("nothing declared"), matching the
    // patch semantics on the write side.
    private static TrackerEntity ToTracker(string id, RawTrackerEntry tracker) =>
        new(
            id,
            EnumMemberName(tracker.Type),
            string.IsNullOrWhiteSpace(tracker.Auth) ? null : tracker.Auth,
            tracker.Url,
            tracker.Organization,
            tracker.Project,
            tracker.OpenStates.Count > 0 ? tracker.OpenStates : null,
            tracker.DoneStatus,
            tracker.FailedStatus,
            tracker.TriggerStatuses.Count > 0 ? tracker.TriggerStatuses : null,
            tracker.PipelineFromLabel is { Count: > 0 } labels ? labels : null,
            tracker.Polling is { } polling
                ? new TrackerPollingSettings(polling.Enabled, polling.IntervalSeconds, polling.JitterPercent)
                : null);

    private static RepoEntity ToRepo(string id, RawRepoEntry repo) =>
        new(id, repo.Url ?? repo.Path ?? string.Empty, repo.DefaultBranch);

    private static ProjectEntity ToProject(string id, RawProjectEntry project)
    {
        var pipelines = project.Pipelines.Count > 0
            ? project.Pipelines.Select(p => p.Name).ToList()
            : !string.IsNullOrWhiteSpace(project.Pipeline) ? [project.Pipeline] : new List<string>();
        return new ProjectEntity(
            id,
            project.Agent,
            project.Tracker,
            project.Repos.Select(r => r.Ref).ToList(),
            string.IsNullOrWhiteSpace(project.Pipeline) ? null : project.Pipeline,
            pipelines,
            ToResolution(project));
    }

    // p0345c: surface the flat resolution shorthand; when the project instead
    // declares a full trigger wrapper, surface ITS resolution read-only so the
    // studio shows how the project actually routes either way.
    private static ProjectResolution? ToResolution(RawProjectEntry project)
    {
        if (project.Resolution is { Count: > 0 } shorthand)
        {
            var first = shorthand.First();
            return new ProjectResolution(first.Key, first.Value);
        }
        var wrapperResolution = new WebhookTriggerConfig?[]
            {
                project.JiraTrigger, project.GithubTrigger,
                project.GitlabTrigger, project.AzuredevopsTrigger,
            }
            .FirstOrDefault(t => t?.ProjectResolution is not null)?.ProjectResolution;
        return wrapperResolution is null
            ? null
            : new ProjectResolution(
                Contracts.Services.ConfigStudioCapabilities.WireName(wrapperResolution.Strategy),
                wrapperResolution.Value);
    }

    private static McpServerEntity ToMcpServer(string id, RawMcpServerEntry mcp) =>
        new(id, mcp.Transport, mcp.Url, mcp.Auth);

    // p0345b: the studio's Organization field is the host-kind's org segment —
    // Azure DevOps organization, GitHub owner, or GitLab group (whichever the
    // raw entry declares). The write direction (FileConfigStore) patches the
    // field matching the connection's type.
    private static ConnectionEntity ToConnection(string id, RawConnectionEntry connection) =>
        new(
            id,
            RepoTypeName(connection.Type),
            connection.Organization ?? connection.Owner ?? connection.Group,
            connection.Project,
            string.IsNullOrWhiteSpace(connection.Auth) ? null : connection.Auth,
            connection.DefaultBranch);

    private static string RepoTypeName(RepoType type) => type switch
    {
        RepoType.GitHub => "github",
        RepoType.GitLab => "gitlab",
        RepoType.AzureDevOps => "azure_devops",
        _ => type.ToString().ToLowerInvariant()
    };

    private static string EnumMemberName(TrackerType type) => type switch
    {
        TrackerType.GitHub => "github",
        TrackerType.GitLab => "gitlab",
        TrackerType.AzureDevOps => "azure_devops",
        TrackerType.Jira => "jira",
        _ => type.ToString().ToLowerInvariant()
    };
}
