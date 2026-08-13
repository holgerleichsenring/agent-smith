using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// Compact overview-view of a run as the broadcaster sees it. One snapshot
/// per active run lives in JobsBroadcaster's active map; finished runs move
/// to the recent ring buffer. Fields are the dashboard contract for the
/// JobUpserted SignalR message.
/// </summary>
public sealed record RunSnapshot(
    string RunId,
    string Pipeline,
    string Trigger,
    IReadOnlyList<string> Repos,
    string Status,
    string? PrUrl,
    string? Summary,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int Sandboxes,
    int StepIndex,
    string? StepName,
    int TotalSteps,
    string? LastEventType,
    decimal CostUsd,
    int LlmCalls,
    // p0363: wall-time decomposition — total LLM call time and how much of it
    // was client-side rate-limiter waiting. Elapsed − LlmDurationMs ≈ sandbox +
    // orchestration; ThrottleWaitMs of LlmDurationMs was pure TPM/RPM queueing.
    // Answers "was that hour real work or waiting?" per run, live.
    long LlmDurationMs = 0,
    long ThrottleWaitMs = 0,
    // p0184: ticket details surfaced by TicketFetchedEvent. Both null until
    // the FetchTicket step lands on the stream; RunCard prefers TicketTitle
    // as the heading and falls back to Pipeline (then "unknown") when absent.
    string? TicketId = null,
    string? TicketTitle = null,
    // p0186: agent display label from RunStartedEvent ("type/model" form,
    // e.g. "claude/claude-sonnet-4-20250514"). Null for pre-p0186 events.
    string? AgentName = null,
    // p0200: flipped true by RunCancelRequestedEvent so the dashboard card
    // can render "cancelling…" until the terminal RunFinished lands.
    bool CancelRequested = false,
    // p0320d: 1-based FIFO position for a status="queued" run, computed at query
    // time from the capacity queue's order (never persisted — the head moves).
    // Null for non-queued runs and on the live SignalR path.
    int? QueuePosition = null,
    // p0332: RESERVED capacity-time for a finished run — memory request x pod
    // lifetime, summed over sandboxes + the spawned orchestrator, in Gi·minutes.
    // Reservation, NOT measured consumption and NOT money: it is what the
    // scheduler set aside for the run. Computed by RunSnapshotMapper from the
    // persisted lifetimes; null while running, on pre-p0332 rows, and on the
    // live SignalR path.
    double? ReservedGiMinutes = null,
    // p0327: the pending DialogQuestion of a status="waiting_for_input" run,
    // joined from its checkpoint row at query time. Null otherwise and on the
    // live SignalR path — the REST refetch (RunsChanged nudge) carries it.
    PendingQuestionInfo? PendingQuestion = null,
    // p0336: the run's capacity calculation (pods + limits + dropped contexts +
    // total vs budget + reservation state), joined from the capacity ledger on
    // the run-detail path. Null on the list + live SignalR path.
    RunFootprintView? Footprint = null,
    // p0344b: server-computed run-story beat states (ticket/plan/building/
    // verify/outcome), derived from the run's typed command progress on BOTH
    // the list and detail paths. Null when the stored data predates the typed
    // step records (the client renders no storybar) and on the live SignalR
    // path — the REST refetch carries it.
    RunBeatsView? Beats = null,
    // p0344b: the p0341 progress ledger persisted at run end, detail-only.
    // Null on the list path, on pre-p0344b rows, and for runs without a ledger.
    IReadOnlyList<ProgressLedgerItemView>? ProgressLedger = null,
    // p0344b: the ratified acceptance criteria + p0340 per-criterion
    // dispositions persisted at run end, detail-only. Null on the list path
    // and for runs without a ratified contract.
    AcceptanceView? Acceptance = null,
    // p0348: the pods the run ACTUALLY spawned, from the persisted RunSandbox
    // rows — the honest "live compute" the side rail shows, distinct from the
    // over-counting reservation in Footprint. Null until the first sandbox lands
    // (client shows "calculating…") and on the live SignalR path.
    RunComputeView? LiveCompute = null,
    // p0350: EVERY pull request the run opened (one per repo). The single PrUrl
    // above is the first opened PR for back-compat; this list carries all of
    // them — a multi-repo run that opens several PRs surfaces each on the
    // Outcome panel instead of collapsing to one. Empty when no PR was opened.
    IReadOnlyList<RunPullRequestView>? PullRequests = null,
    // p0355: the TYPED cancel reason (operator / stale-lease-reaped / watchdog-wall-
    // time / budget / crashed / sandbox-vanished) so the UI can distinguish a reap
    // (owning replica gone) from an operator cancel instead of collapsing both to
    // "cancelled by operator". Null when the run was not cancelled.
    string? CancelReason = null,
    // p0357: the resolved cost budget (RunBudgetResolvedEvent from ScopeRepos) —
    // complexity tier + cap so the client renders CostUsd against a denominator.
    // Null before ScopeRepos lands, on Unknown-tier runs, and on pre-p0357 rows.
    string? BudgetTier = null,
    decimal? BudgetCapUsd = null,
    long? BudgetCapTokens = null,
    // p0369: the per-run metrics summary (time split, tool usage, redundant
    // reads/writes, cache health, build/test) served on the run DETAIL — WHERE
    // the run's time and cost went. Null on the list + live SignalR path and on
    // pre-p0369 rows / runs with no folded events yet.
    RunMetricsView? Metrics = null,
    // p0404: the run's wall-clock split — model, throttle, sandbox, scaffolding —
    // rolled up from the per-step attribution the applier persists. Detail-only
    // and null on the live SignalR path; null too for runs whose steps carry no
    // attributed time (pre-p0404 rows), so the client shows nothing rather than
    // a zero that reads as "no model time".
    RunTimeSplitView? TimeSplit = null)
{
    /// <summary>
    /// p0211: explicit, stable run title for the dashboard. Resolves to the
    /// real ticket title when the FetchTicket step has landed; otherwise a
    /// deterministic "{Pipeline} #{TicketId}" label (or just the pipeline when
    /// no ticket). Never the literal "unknown"/empty once the pipeline is
    /// known — this is an explicit fallback for a not-yet-fetched title, not a
    /// heuristic for genuinely-missing data.
    /// </summary>
    public string Title =>
        !string.IsNullOrWhiteSpace(TicketTitle) ? TicketTitle!
        : !string.IsNullOrWhiteSpace(TicketId) ? $"{Pipeline} #{TicketId}"
        : Pipeline;

    public static RunSnapshot Empty(string runId) => new(
        runId, "unknown", "unknown", Array.Empty<string>(),
        "running", null, null,
        DateTimeOffset.UtcNow, null, 0, 0, null, 0, null,
        CostUsd: 0m, LlmCalls: 0);

}
