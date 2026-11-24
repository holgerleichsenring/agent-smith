namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-24-297b: the newest refresh-base run of one pull request whatever it ended with — at work (queued or running) or
/// not, and when it started — so a conflict is handed to one refresh, not to one per read.
/// </summary>
public sealed record PullRequestRefreshTried(string RunId, bool InFlight, DateTimeOffset At)
{
    /// <summary>Whether a conflict that began at <paramref name="conflictsSince"/> still waits for a refresh of its own: none
    /// is at work and this one started before the conflict did.</summary>
    public bool LeavesDue(DateTimeOffset conflictsSince) => !InFlight && At < conflictsSince;
}
