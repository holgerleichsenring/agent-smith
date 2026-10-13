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
    IReadOnlyList<TicketPullRequestView> PullRequests);
