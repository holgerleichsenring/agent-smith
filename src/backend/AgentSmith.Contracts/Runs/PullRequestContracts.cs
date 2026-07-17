namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0347: one per-repo pull-request outcome as persisted on the run row
/// (Runs.PullRequestsJson) and served verbatim on the run detail — the same
/// stored-JSON-is-the-wire-JSON pattern p0344b uses for the run story. Projected
/// from <c>PullRequestOutcomeEvent</c> by RunEventApplier, upserted by repo (last
/// outcome per repo wins), so a multi-repo run keeps EVERY repo's PR — unlike the
/// single lossy Run.PrUrl. Status vocabulary: "opened" | "no_changes" | "failed".
/// <see cref="Url"/> is the created PR URL (opened only); <see cref="Reason"/> the
/// failure reason (failed only); <see cref="OpenedAt"/> the outcome timestamp.
/// </summary>
public sealed record RunPullRequestView(
    string Repo,
    string Status,
    string? Url,
    string? Reason,
    DateTimeOffset OpenedAt);

/// <summary>
/// p0347: one flattened row of the Pull Requests page (GET /api/pull-requests) —
/// a per-repo PR outcome joined to the run/ticket facts that produced it, newest
/// first. Old runs (pre-migration, no PullRequestsJson) contribute a single row
/// from the run's lone PR url so history isn't blank.
/// </summary>
public sealed record PullRequestListItem(
    string RunId,
    string? TicketId,
    string? TicketTitle,
    string Pipeline,
    string? Repo,
    string Status,
    string? Url,
    string? Reason,
    DateTimeOffset OpenedAt);
