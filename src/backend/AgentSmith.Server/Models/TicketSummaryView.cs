namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59a: one ticket on the ticket list — a (project, raw ticket id) pair with at
/// least one code-pipeline run, computed from its runs on every read and never stored.
/// </summary>
/// <param name="TicketKey">The spelling a bound conversation stores; null when no run carries a
/// platform and the project is no longer configured.</param>
/// <param name="Kind">question or handback on a needs-you row; null otherwise.</param>
/// <param name="Since">When the current state began: the question's asked-at, the attempt's start,
/// or its end. Null for a hand-back, whose park moment is not stored.</param>
/// <param name="NewestRunId">The newest attempt, or the newest run when none counts as one.</param>
/// <param name="LastOutcome">The newest attempt's status on an idle row; null otherwise.</param>
/// <param name="Attempts">Every attempt, oldest first.</param>
/// <param name="PullRequests">2026-10-13-9a59b: every pull request the ticket's runs named, from its record.</param>
/// <param name="Cause">2026-10-13-9a59d: why the newest attempt with a record began.</param>
/// <param name="TrackerState">2026-10-13-9a59d: the tracker state that attempt fetched.</param>
/// <param name="WebUrl">2026-10-13-9a59d: where the ticket opens in its tracker.</param>
/// <param name="SeriesStrip">2026-10-13-9a59e: the series at a glance; set on list rows only.</param>
/// <param name="UnchangedSinceLastAttempt">2026-10-13-9a59h: true when a fresh check saw a failed last attempt's
/// ticket unchanged and untouched, false when it saw an act or an edit, null when nothing observed it.</param>
/// <param name="CheckedAt">2026-10-13-9a59h: when that check ran; null when none did.</param>
/// <param name="Handback">2026-10-13-9a59h: the posted hand-back's words and time on a hand-back row.</param>
public sealed record TicketSummaryView(
    string Project,
    string TicketId,
    string? TicketKey,
    string? Platform,
    string? Title,
    string Bucket,
    string? Kind,
    DateTimeOffset? Since,
    int AttemptCount,
    decimal Cost,
    string NewestRunId,
    string Status,
    string? LastOutcome,
    int? QueuePlace,
    TicketPendingQuestion? PendingQuestion,
    int SpecsDone,
    int SpecsTotal,
    IReadOnlyList<TicketAttemptMark> Attempts,
    IReadOnlyList<TicketPullRequestView> PullRequests,
    AgentSmith.Contracts.Runs.AttemptCause? Cause = null,
    string? TrackerState = null,
    string? WebUrl = null,
    TicketSeriesStripView? SeriesStrip = null,
    bool? UnchangedSinceLastAttempt = null,
    DateTimeOffset? CheckedAt = null,
    TicketHandbackView? Handback = null);
