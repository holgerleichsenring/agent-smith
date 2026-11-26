namespace AgentSmith.Contracts.Runs;

/// <summary>2026-11-17-3435b: one repository's pull request a merge-green run merges.</summary>
public sealed record CiGreenPullRequest(string Repo, string Url)
{
    /// <summary>2026-11-24-f9c5d: a pull request into a series branch whose head reported no CI for merge.ci_settle_seconds —
    /// the run starts its repository's pull-request CI before anything merges.</summary>
    public bool NoCi { get; init; }

    /// <summary>2026-11-24-629aa: a pull request whose required build has not started (or expired) on its head for
    /// merge.ci_settle_seconds — the run queues it again and merges nothing.</summary>
    public bool NotStarted { get; init; }
}
