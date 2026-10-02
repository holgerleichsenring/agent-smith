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
            Projects: raw.Projects.Select(kv => ProjectEntityMapping.ToProject(kv.Key, kv.Value)).ToList(),
            McpServers: raw.McpServers.Select(kv => ToMcpServer(kv.Key, kv.Value)).ToList(),
            Secrets: raw.Secrets.Keys.Select(k => new SecretEntity(k)).ToList(),
            Connections: raw.Connections.Select(kv => ToConnection(kv.Key, kv.Value)).ToList(),
            DesignSources: raw.DesignSources.Select(kv => DesignSourceEntityMapping.ToEntity(kv.Key, kv.Value)).ToList());

    // p0345c: the FULL raw agent surface. 2026-09-30-62bab: models surface in catalog form —
    // AgentCatalogProjection turns inline roles and the agent's own model into entries — and
    // each role the operator SET names its entry; an unset role inherits and is absent.
    // Sections not surfaced here (parallelism, rate limit, loop tuning) survive upsert
    // untouched via the patch builders.
    private static AgentEntity ToAgent(string id, AgentConfig agent)
    {
        var (catalog, models) = AgentCatalogProjection.Of(agent);
        return new AgentEntity(
            id,
            agent.Type,
            agent.ApiKeySecret,
            agent.Endpoint,
            agent.ApiVersion,
            agent.NetworkTimeoutSeconds,
            catalog,
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
                agent.Compaction.KeepRecentIterations),
            new AgentRetrySettings(
                agent.Retry.MaxRetries,
                agent.Retry.InitialDelayMs,
                agent.Retry.BackoffMultiplier,
                agent.Retry.MaxDelayMs));
    }

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
                : null,
            tracker.NeedsClarificationStatus,
            tracker.NotImplementableStatus,
            tracker.CloseTransitionName,
            tracker.ExtraFields.Count > 0 ? tracker.ExtraFields : null,
            tracker.ZeroMatchComment,
            tracker.LifecycleStatusNames is { Count: > 0 } lifecycle ? lifecycle : null,
            tracker.DefaultPipeline,
            tracker.WorkItemKinds is { Count: > 0 } kinds ? kinds : null,
            tracker.LabelNames is { Count: > 0 } labelNames ? labelNames : null,
            JiraEndpointsMap.Overrides(tracker.Endpoints));

    private static RepoEntity ToRepo(string id, RawRepoEntry repo) =>
        new(id, repo.Url ?? repo.Path ?? string.Empty, repo.DefaultBranch);

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
            connection.DefaultBranch,
            connection.Host);

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
