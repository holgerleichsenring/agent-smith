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
    long? BudgetCapTokens = null)
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

    public RunSnapshot Apply(RunEvent runEvent) => runEvent switch
    {
        RunStartedEvent e => this with
        {
            Pipeline = e.Pipeline, Trigger = e.Trigger, Repos = e.Repos,
            Status = "running", StartedAt = e.StartedAt, LastEventType = e.Type.ToString(),
            AgentName = e.AgentName ?? AgentName,
            // p0211: ticket id at run start feeds the title fallback label
            // before any TicketFetchedEvent (and for runs that never fetch one).
            TicketId = e.TicketId ?? TicketId,
        },
        // p0176b: RunFinished.CostUsd, when present, overrides the per-call
        // accumulation. Defence in depth: even if a producer leaked LLM
        // calls past the factory wrap, the run-end truth lands here.
        RunFinishedEvent e => this with
        {
            Status = e.Status, PrUrl = e.PrUrl, Summary = e.Summary,
            FinishedAt = e.FinishedAt,
            CostUsd = e.CostUsd ?? CostUsd,
            LastEventType = e.Type.ToString()
        },
        SandboxCreatedEvent => this with
        {
            Sandboxes = Sandboxes + 1, LastEventType = runEvent.Type.ToString()
        },
        StepStartedEvent e => this with
        {
            StepIndex = e.StepIndex, StepName = e.StepName, TotalSteps = e.TotalSteps,
            LastEventType = e.Type.ToString()
        },
        StepFinishedEvent e => this with
        {
            LastEventType = e.Type.ToString()
        },
        // p0175-fix: LLM cost rolls up onto the run snapshot so the
        // /system CostRollupCard can read it from the overview without
        // a separate cross-stream subscription. Per-event granularity
        // is preserved in the run-stream; snapshot keeps the running
        // total for fast dashboard reads.
        LlmCallFinishedEvent e => this with
        {
            CostUsd = CostUsd + (decimal)e.CostUsd,
            LlmCalls = LlmCalls + 1,
            LastEventType = e.Type.ToString()
        },
        // p0357: the resolved budget lands live on the snapshot — the runs page
        // shows spent/cap without waiting for the REST refetch.
        RunBudgetResolvedEvent e => this with
        {
            BudgetTier = e.Tier, BudgetCapUsd = e.CapUsd, BudgetCapTokens = e.CapTokens,
            LastEventType = e.Type.ToString()
        },
        // p0184: copy ticket id + title onto the snapshot so the runs-page
        // card has the human-readable heading at-a-glance. Description /
        // attachments stay on the event for the Fetch-ticket step body to
        // read on drill-in.
        TicketFetchedEvent e => this with
        {
            TicketId = e.TicketId,
            TicketTitle = e.Title,
            LastEventType = e.Type.ToString()
        },
        // p0200: cancel-requested flips the snapshot bit; the terminal
        // RunFinished still drives the move from Active to Recent.
        RunCancelRequestedEvent e => this with
        {
            CancelRequested = true,
            CancelReason = e.Reason,
            LastEventType = e.Type.ToString()
        },
        // p0350: an opened PR now lands on the LIVE snapshot too (was trail-only,
        // so the live card showed no PR until the REST refetch). Accumulate per
        // repo and seed the primary PrUrl. Draft-ness is only known at run end, so
        // the live view marks non-draft; the REST refetch (RunSnapshotMapper)
        // carries the authoritative flag.
        PullRequestOutcomeEvent e when e.Status == "opened" && !string.IsNullOrEmpty(e.Url) => this with
        {
            PrUrl = PrUrl ?? e.Url,
            PullRequests = AppendPr(PullRequests, new RunPullRequestView(e.Repo, e.Url!, e.Status, IsDraft: false)),
            LastEventType = e.Type.ToString()
        },
        _ => this with { LastEventType = runEvent.Type.ToString() }
    };

    // p0350: upsert a PR by repo (a repeat outcome for the same repo replaces,
    // never duplicates) so the live list mirrors the per-repo DB rows.
    private static IReadOnlyList<RunPullRequestView> AppendPr(
        IReadOnlyList<RunPullRequestView>? existing, RunPullRequestView pr)
    {
        var list = existing is null
            ? new List<RunPullRequestView>()
            : existing.Where(p => p.Repo != pr.Repo).ToList();
        list.Add(pr);
        return list;
    }
}
