namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-23-0219: queues a pull request's required builds again where they have not started on its head and will not by
/// themselves (<see cref="CiStates.NotStarted"/>) — Azure DevOps runs no build-validation policy on a draft, and after the
/// pull request is published or retargeted its build may never start. Implemented by the source providers whose host has
/// such builds, reached by casting the repo's provider, as <see cref="IPullRequestCiReader"/> is.
/// <para>2026-11-25-7a92b: GitHub re-runs a stale Actions run; GitLab starts a pipeline for a head without one, through the
/// pull-request CI start f9c5d built. Neither queues a pull request from another repository.</para>
/// </summary>
public interface IPullRequestBuildRequeuer
{
    /// <summary>Queues every not-started required build again; returns how many were queued. <paramref name="headSettled"/>
    /// (2026-11-25-7a92b): the caller vouches the head has stood for merge.ci_settle_seconds, so a host may read "nothing on
    /// this head" as not started (<see cref="CiReadRequest.HeadSettled"/>); right after a push it is false.</summary>
    Task<int> RequeueNotStartedAsync(string prUrl, bool headSettled, CancellationToken cancellationToken);
}
