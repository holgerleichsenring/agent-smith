namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Issue/work-item tracker connection, materialized from a named catalog entry.
/// Name is the catalog key. Lifecycle fields (OpenStates, DoneStatus,
/// CloseTransitionName, ExtraFields) belong here when shared across projects.
/// </summary>
public sealed record TrackerConnection
{
    public string Name { get; init; } = string.Empty;
    public TrackerType Type { get; init; } = TrackerType.GitHub;
    public string? Url { get; init; }
    public string? Organization { get; init; }
    public string? Project { get; init; }
    public string Auth { get; init; } = string.Empty;
    public IReadOnlyList<string> OpenStates { get; init; } = [];
    public string? DoneStatus { get; init; }
    public string? CloseTransitionName { get; init; }
    public IReadOnlyList<string> ExtraFields { get; init; } = [];

    /// <summary>
    /// p0281b: tracker-owned run-trigger gate, shared by every project routed to this tracker.
    /// Projects override field-by-field via their own trigger block; an unset project gate
    /// inherits this (falling back to <see cref="OpenStates"/> when this too is empty).
    /// </summary>
    public IReadOnlyList<string> TriggerStatuses { get; init; } = [];

    /// <summary>p0281b: tracker-owned failed_status base; a project trigger overrides it.</summary>
    public string? FailedStatus { get; init; }

    /// <summary>p0281b: tracker-owned label→pipeline map; a project trigger overrides it.</summary>
    public IReadOnlyDictionary<string, string>? PipelineFromLabel { get; init; }

    /// <summary>
    /// p0140a: when true (and the provider supports comments), zero-match webhook routing posts a
    /// 'no agent-smith project matched this ticket' comment to the ticket. Default false because
    /// multi-tenant trackers (most realistic deployments) would generate noise. Single-tenant
    /// operators can opt in for forensic visibility. The runtime guard lives in p0140b's
    /// webhook handlers.
    /// </summary>
    public bool ZeroMatchComment { get; init; } = false;

    /// <summary>
    /// p0140c: per-tracker polling cadence. Pollers go per-tracker after p0140c so the polling
    /// settings move from ResolvedProject.Polling (deprecated) to here. Disabled by default so
    /// trackers used only as webhook destinations don't trigger polling.
    /// </summary>
    public PollingConfig Polling { get; init; } = new();

    /// <summary>
    /// Jira-only: operator-overridable REST endpoint templates (YAML key <c>endpoints</c>).
    /// Defaults target Jira Cloud v3; override when Atlassian changes an API path so the fix
    /// is config, not a redeploy. Ignored for non-Jira trackers.
    /// </summary>
    public JiraEndpoints Endpoints { get; init; } = new();
}
