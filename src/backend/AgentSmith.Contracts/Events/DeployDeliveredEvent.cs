namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-18-3863f: a deploy run delivered — the pull request it opened on <paramref name="Repo"/> (the repository the
/// delivery went to, by its configured name), or the pipeline run it started there. <paramref name="PipelineRunId"/> is
/// null for a pull request and for a pipeline run the host did not name, which cannot be followed. The run row family
/// inserts a deploy record from it; the server follows that record until it settles.
/// </summary>
public sealed record DeployDeliveredEvent(
    string RunId,
    DateTimeOffset Timestamp,
    string Project,
    string Context,
    string? ContextRepo,
    string Environment,
    string Version,
    string Delivery,
    string Repo,
    string Url,
    string? PullRequestUrl,
    string? PipelineRunId)
    : RunEvent(RunId, EventType.DeployDelivered, Timestamp);
