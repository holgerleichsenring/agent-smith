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
    /// p0140a: when true (and the provider supports comments), zero-match webhook routing posts a
    /// 'no agent-smith project matched this ticket' comment to the ticket. Default false because
    /// multi-tenant trackers (most realistic deployments) would generate noise. Single-tenant
    /// operators can opt in for forensic visibility. The runtime guard lives in p0140b's
    /// webhook handlers.
    /// </summary>
    public bool ZeroMatchComment { get; init; } = false;
}
