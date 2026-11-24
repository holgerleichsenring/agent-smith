namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-24-6811d: what the newest refresh-base run of one pull request says — still at work on it (queued or running),
/// or it left the branch unchanged, with the reason (the refusal it recorded, or the failed run's summary) — with the run
/// and when it started. A refresh that pushed or found nothing to do says nothing and is absent.
/// </summary>
public sealed record PullRequestRefreshSaid(string RunId, bool InFlight, string? Reason, DateTimeOffset At)
{
    /// <summary>2026-11-24-9dc0e: the pull request it names, set where several open ones are read together.</summary>
    public string? Url { get; init; }

    /// <summary>At work, or a refusal no head move has overtaken since the run began.</summary>
    public bool Stands(DateTimeOffset? headMovedAt) => InFlight || headMovedAt is not { } moved || moved <= At;
}
