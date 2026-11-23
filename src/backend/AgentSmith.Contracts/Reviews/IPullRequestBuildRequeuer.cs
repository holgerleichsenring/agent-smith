namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-23-0219: queues a pull request's required builds again where they have not started on its head and will not by
/// themselves (<see cref="CiStates.NotStarted"/>) — Azure DevOps runs no build-validation policy on a draft, and after the
/// pull request is published or retargeted its build may never start. Implemented by the source providers whose host has
/// such builds, reached by casting the repo's provider, as <see cref="IPullRequestCiReader"/> is.
/// </summary>
public interface IPullRequestBuildRequeuer
{
    /// <summary>Queues every not-started required build again; returns how many were queued.</summary>
    Task<int> RequeueNotStartedAsync(string prUrl, CancellationToken cancellationToken);
}
