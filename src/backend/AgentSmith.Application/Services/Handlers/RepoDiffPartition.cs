using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0355: the outcome of partitioning a run's repos by working-tree diff —
/// the sandboxes of the repos that actually changed (the only ones the
/// post-execute passes may touch), their repo names in scope order, and the
/// names of the repos skipped because nothing in them changed.
/// </summary>
public sealed record RepoDiffPartition(
    IReadOnlyDictionary<string, ISandbox> ChangedSandboxes,
    IReadOnlyList<string> ChangedRepoNames,
    IReadOnlyList<string> SkippedRepoNames);
