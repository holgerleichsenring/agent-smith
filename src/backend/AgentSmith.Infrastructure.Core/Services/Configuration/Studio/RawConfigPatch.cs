using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0349: the studio-entity -&gt; raw-config patch builders, shared by every store
/// (file + DB). A patch PRESERVES the sections the entity leaves null on the
/// existing raw entry (parallelism, rate limits, the compaction token-ratio
/// trigger, deployment overrides), so an export still round-trips through the real
/// loader whichever store applied the edit. Extracted from FileConfigStore so the
/// DB store applies the identical semantics — not a second, drifting copy.
/// </summary>
public static class RawConfigPatch
{
    public static AgentConfig Agent(AgentEntity entity, AgentConfig? existing)
    {
        var agent = existing ?? new AgentConfig();
        agent.Type = entity.Provider;
        agent.ApiKeySecret = entity.KeySecret;
        agent.Endpoint = entity.Endpoint;
        agent.ApiVersion = entity.ApiVersion;
        if (entity.NetworkTimeoutSeconds is { } timeout) agent.NetworkTimeoutSeconds = timeout;

        RawAgentModelPatch.Apply(entity, agent);
        if (entity.Pricing is { } pricing)
            agent.Pricing.Models = pricing.Models.ToDictionary(
                kv => kv.Key,
                kv => new ModelPricing
                {
                    InputPerMillion = kv.Value.InputPerMillion,
                    OutputPerMillion = kv.Value.OutputPerMillion,
                    CacheReadPerMillion = kv.Value.CacheReadPerMillion ?? 0m,
                });
        if (entity.Cache is { } cache)
        {
            agent.Cache.IsEnabled = cache.IsEnabled;
            agent.Cache.Strategy = cache.Strategy;
        }
        if (entity.Compaction is { } compaction)
        {
            agent.Compaction.IsEnabled = compaction.IsEnabled;
            agent.Compaction.ThresholdIterations = compaction.ThresholdIterations;
            agent.Compaction.MaxContextTokens = compaction.MaxContextTokens;
            agent.Compaction.KeepRecentIterations = compaction.KeepRecentIterations;
            agent.Compaction.SummaryModel = compaction.SummaryModel;
        }
        if (entity.Retry is { } retry)
        {
            agent.Retry.MaxRetries = retry.MaxRetries;
            agent.Retry.InitialDelayMs = retry.InitialDelayMs;
            agent.Retry.BackoffMultiplier = retry.BackoffMultiplier;
            agent.Retry.MaxDelayMs = retry.MaxDelayMs;
        }
        return agent;
    }

    public static RawTrackerEntry Tracker(TrackerEntity entity, RawTrackerEntry? existing)
    {
        var tracker = existing ?? new RawTrackerEntry();
        tracker.Type = ParseEnum(entity.Type, TrackerType.GitHub);
        tracker.Url = entity.Url;
        tracker.Organization = entity.Organization;
        tracker.Project = entity.Project;
        tracker.Auth = entity.AuthSecret ?? string.Empty;
        if (entity.OpenStates is { } openStates) tracker.OpenStates = [.. openStates];
        tracker.DoneStatus = entity.DoneStatus;
        tracker.FailedStatus = entity.FailedStatus;
        if (entity.TriggerStatuses is { } triggerStatuses) tracker.TriggerStatuses = [.. triggerStatuses];
        if (entity.PipelineFromLabel is { } labels)
            tracker.PipelineFromLabel = labels.ToDictionary(kv => kv.Key, kv => kv.Value);
        // p0392: the workflow fields the studio could not reach. Null keeps the stored
        // value (patch semantics); the park status is the one the 2026-07-31 outage needed.
        tracker.NeedsClarificationStatus = entity.NeedsClarificationStatus;
        tracker.NotImplementableStatus = entity.NotImplementableStatus;
        tracker.CloseTransitionName = entity.CloseTransitionName;
        if (entity.ExtraFields is { } extraFields) tracker.ExtraFields = [.. extraFields];
        if (entity.ZeroMatchComment is { } zeroMatch) tracker.ZeroMatchComment = zeroMatch;
        if (entity.LifecycleStatusNames is { } lifecycle)
            tracker.LifecycleStatusNames = lifecycle.ToDictionary(kv => kv.Key, kv => kv.Value);
        if (entity.Polling is { } polling)
            tracker.Polling = new RawPollingEntry
            {
                Enabled = polling.Enabled,
                IntervalSeconds = polling.IntervalSeconds,
                JitterPercent = polling.JitterPercent,
            };
        return tracker;
    }

    public static RawRepoEntry Repo(RepoEntity entity, RawRepoEntry? existing)
    {
        var repo = existing ?? new RawRepoEntry();
        if (entity.Name.Contains("://"))
        {
            repo.Url = entity.Name;
            if (existing is null) repo.Type = InferRepoType(entity.Name);
        }
        else
        {
            repo.Path = entity.Name;
            if (existing is null) repo.Type = RepoType.Local;
        }
        repo.DefaultBranch = entity.Branch;
        return repo;
    }

    public static RawProjectEntry Project(ProjectEntity entity, RawProjectEntry? existing)
    {
        var project = existing ?? new RawProjectEntry();
        project.Agent = entity.Agent;
        project.Tracker = entity.Tracker;
        project.Repos = entity.Repos.Select(r => new RawRepoRef(r)).ToList();
        if (!string.IsNullOrWhiteSpace(entity.Pipeline)) project.Pipeline = entity.Pipeline!;
        if (entity.DefaultPipeline is not null) // p0392
            project.DefaultPipeline = string.IsNullOrWhiteSpace(entity.DefaultPipeline)
                ? null : entity.DefaultPipeline;
        if (entity.Resolution is { } resolution)
            project.Resolution = new Dictionary<string, string> { [resolution.Strategy] = resolution.Value };
        if (entity.Pipelines.Count > 0)
            project.Pipelines = entity.Pipelines
                .Select(name => existing?.Pipelines.FirstOrDefault(p => p.Name == name) ?? new RawPipelineEntry { Name = name })
                .ToList();
        return project;
    }

    public static RawConnectionEntry Connection(ConnectionEntity entity, RawConnectionEntry? existing)
    {
        var connection = existing ?? new RawConnectionEntry();
        connection.Type = ParseEnum(entity.Type, RepoType.GitHub);
        connection.Organization = connection.Type == RepoType.AzureDevOps ? entity.Organization : null;
        connection.Owner = connection.Type == RepoType.GitHub ? entity.Organization : null;
        connection.Group = connection.Type == RepoType.GitLab ? entity.Organization : null;
        connection.Project = connection.Type == RepoType.AzureDevOps ? entity.Project : null;
        connection.Auth = entity.AuthSecret ?? string.Empty;
        connection.DefaultBranch = entity.DefaultBranch;
        connection.Host = entity.Host; // p0392: self-hosted base URL
        return connection;
    }

    public static RawMcpServerEntry Mcp(McpServerEntity entity, RawMcpServerEntry? existing)
    {
        var mcp = existing ?? new RawMcpServerEntry();
        mcp.Transport = entity.Transport;
        mcp.Url = entity.Url;
        mcp.Auth = entity.AuthSecret;
        return mcp;
    }

    private static RepoType InferRepoType(string url) =>
        url.Contains("github", StringComparison.OrdinalIgnoreCase) ? RepoType.GitHub
        : url.Contains("gitlab", StringComparison.OrdinalIgnoreCase) ? RepoType.GitLab
        : url.Contains("dev.azure", StringComparison.OrdinalIgnoreCase)
            || url.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase) ? RepoType.AzureDevOps
        : RepoType.GitHub;

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct, Enum
    {
        var normalized = value.Replace("_", string.Empty);
        return Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsed) ? parsed : fallback;
    }
}
