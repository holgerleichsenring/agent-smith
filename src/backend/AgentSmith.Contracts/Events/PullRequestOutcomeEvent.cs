namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0223: the per-repo outcome of the commit/PR step, surfaced so the run detail
/// can render a meaningful result instead of the raw "git commit · exit 1"
/// sandbox row. <see cref="Status"/> is "opened" | "no_changes" | "failed".
/// <see cref="Url"/> carries the created PR URL when opened; <see cref="Reason"/>
/// carries the real failure reason when status is "failed". A no-changes outcome
/// is a normal, expected result (nothing to commit), not a failure.
/// </summary>
public sealed record PullRequestOutcomeEvent(
    string RunId,
    string Repo,
    string Status,
    DateTimeOffset Timestamp,
    string? Url = null,
    string? Reason = null)
    : RunEvent(RunId, EventType.PullRequestOutcome, Timestamp);
