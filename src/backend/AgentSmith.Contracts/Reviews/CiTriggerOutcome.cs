namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-11-24-f9c5d: what asking a host to run a pull request's CI came to.</summary>
public enum CiTriggerOutcome
{
    /// <summary>The CI was started (queued, dispatched, created) or the host starts it itself; its checks follow.</summary>
    Started,

    /// <summary>The repository runs no pull-request CI for its default branch either — nothing to wait for.</summary>
    NoCi,

    /// <summary>The repository has pull-request CI, but it cannot be started for this pull request; the text says why.</summary>
    Impossible,
}
