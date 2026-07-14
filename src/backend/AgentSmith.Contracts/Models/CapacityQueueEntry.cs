namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0320c: what the spawn funnel offers to <see cref="Services.ICapacityQueue"/>.
/// CandidateRunId is a freshly generated run id — used (and stored as the entry's
/// ReservedRunId) only when this ticket is not queued yet; otherwise the existing
/// reservation wins and the candidate id is discarded. Repos seed the queued Run
/// row's repo children so the dashboard shows what the run will touch.
/// InitialContextJson/PlanAnswersJson carry the trigger envelope so the pump can
/// re-claim without a fresh webhook/poll envelope.
/// </summary>
public sealed record CapacityQueueCandidate(
    string Project,
    string TicketId,
    string Pipeline,
    string Platform,
    string CandidateRunId,
    string Reason,
    IReadOnlyList<string> Repos,
    string? InitialContextJson,
    string? PlanAnswersJson,
    // p0327: a resume of a checkpointed run. Its Run row ALREADY exists
    // (waiting_for_input) — the enqueue must not create a queued row, and the
    // pump launches it without re-validating trigger statuses (the ticket sits
    // legitimately in its working status mid-run).
    bool IsResume = false);

/// <summary>
/// p0320c: one persisted capacity-queue entry. ReservedRunId points at the single
/// "queued" Run row that becomes the running row on launch. A null
/// InitialContextJson marks a TOCTOU-backstop entry (projected from a
/// RunFinished status="queued" event) — those are re-launched by the poller
/// funnel with a fresh envelope, never by the pump.
/// </summary>
public sealed record CapacityQueueEntry(
    string Project,
    string TicketId,
    string Pipeline,
    string Platform,
    string? ReservedRunId,
    string Reason,
    DateTimeOffset EnqueuedAt,
    string? InitialContextJson,
    string? PlanAnswersJson,
    // p0327: resume entry — launched by the pump's resume path (lease + direct
    // job enqueue, no ticket lifecycle transition, no trigger-status check).
    bool IsResume = false);
