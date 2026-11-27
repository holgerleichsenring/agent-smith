namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0223: the per-repo outcome of the commit/PR step, surfaced so the run detail
/// can render a meaningful result instead of the raw "git commit · exit 1"
/// sandbox row. <see cref="Status"/> is "opened" | "no_changes" | "failed".
/// <see cref="Url"/> carries the created PR URL when opened; <see cref="Reason"/>
/// carries the real failure reason when status is "failed". A no-changes outcome
/// is a normal, expected result (nothing to commit), not a failure.
/// 2026-11-27-ecf2b: <see cref="CarriesCode"/> says whether an opened pull request carries code or only the spec
/// record; null when the run could not tell (and on every outcome before it).
/// </summary>
public sealed record PullRequestOutcomeEvent(
    string RunId,
    string Repo,
    string Status,
    DateTimeOffset Timestamp,
    string? Url = null,
    string? Reason = null,
    bool? CarriesCode = null)
    : RunEvent(RunId, EventType.PullRequestOutcome, Timestamp);
