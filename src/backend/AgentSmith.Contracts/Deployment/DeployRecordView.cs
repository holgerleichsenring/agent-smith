using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Contracts.Deployment;

/// <summary>
/// 2026-11-18-3863f: where a deploy stands, as its run's page and its ticket show it — what went where, the link to
/// follow, the pull request's state (null for a pipeline), the CI or pipeline state with a red one's failures, and
/// whether it settled; <see cref="Note"/> says why following stopped when that was not because it settled on its own.
/// </summary>
public sealed record DeployRecordView(
    string RunId,
    string Project,
    string Context,
    string Environment,
    string Version,
    string Delivery,
    string Repo,
    string Url,
    string? PullRequestState,
    string CiState,
    IReadOnlyList<CiFailure> Failures,
    DateTimeOffset DeliveredAt,
    DateTimeOffset? CheckedAt,
    DateTimeOffset? SettledAt,
    string? Note);
