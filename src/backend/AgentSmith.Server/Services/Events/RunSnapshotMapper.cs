using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0246f: maps a persisted <see cref="Run"/> (read from the DB via RunRepository)
/// to the dashboard's <see cref="RunSnapshot"/> contract — so the run list/detail
/// can be served from the system-of-record, surviving a process restart and a
/// Redis flush, not just from the in-memory broadcaster snapshots.
/// </summary>
public static class RunSnapshotMapper
{
    // p0320d: queuePosition carries the run's 1-based FIFO rank when it is a
    // capacity-queued row (matched via QueuedTicket.ReservedRunId at query time).
    // p0332: orchestratorMemoryRequest is the JobSpawner Resources memory-request
    // the spawner uses for the orchestrator pod; null falls back to the spawner's
    // own unconfigured default (ResourceLimits.Default).
    // p0327: pendingQuestion carries the parked run's DialogQuestion (joined from
    // its checkpoint row at query time) so the dashboard can render the answer
    // affordance for status="waiting_for_input".
    // p0344b: includeStory=true (the run-detail path) additionally serves the
    // persisted progress ledger + acceptance snapshot; beats ride BOTH paths.
    public static RunSnapshot ToSnapshot(
        Run run, int? queuePosition = null, string? orchestratorMemoryRequest = null,
        PendingQuestionInfo? pendingQuestion = null, RunCapacitySnapshot? capacity = null,
        bool includeStory = false)
    {
        var lastStep = run.Steps.OrderByDescending(s => s.StepIndex).FirstOrDefault();
        // p0350: ALL opened PRs (draft or ready), not just the first — a multi-repo
        // run opens several and they must all surface on the Outcome panel. Draft-
        // ness is shared across a run's PRs (a red/keystone-unsatisfied run opens
        // drafts), so it is derived from the terminal status. PrUrl stays = the
        // first opened PR for back-compat with the single-link surfaces.
        var openedPrs = run.Repos
            .Where(r => r.PrStatus == "opened" && !string.IsNullOrEmpty(r.PrUrl))
            .Select(r => new RunPullRequestView(r.RepoName, r.PrUrl!, r.PrStatus!, IsDraft: run.Status != "success"))
            .ToList();
        var openedPr = run.Repos.FirstOrDefault(r => r.PrStatus == "opened");
        // p0404: the run's time split, rolled up from what its steps carry. Read
        // once — the top-level LlmDurationMs/ThrottleWaitMs pair is the same
        // measurement the drawer's split is made of, so they cannot disagree.
        var timeSplit = RunTimeRollup.From(run.Steps);
        return new RunSnapshot(
            RunId: run.Id,
            Pipeline: run.Pipeline,
            Trigger: run.Trigger ?? "unknown",
            Repos: run.Repos.Select(r => r.RepoName).ToList(),
            Status: run.Status,
            PrUrl: openedPr?.PrUrl,
            Summary: run.Summary,
            StartedAt: run.StartedAt,
            FinishedAt: run.FinishedAt,
            Sandboxes: run.Sandboxes.Count,
            StepIndex: lastStep?.StepIndex ?? 0,
            StepName: lastStep?.DisplayName ?? lastStep?.StepName,
            // p0322a: prefer the persisted producer total (RunEventApplier keeps the
            // max StepStartedEvent.TotalSteps seen) so an in-flight run renders real
            // x/y progress; pre-migration rows fall back to the steps seen (exact
            // once finished; a lower bound while running).
            TotalSteps: run.TotalSteps ?? run.Steps.Count,
            LastEventType: null,
            CostUsd: run.CostTotalUsd,
            LlmCalls: run.LlmCalls.Count,
            // p0404: a FINISHED run used to report 0 here — the live fold lives on
            // the broadcaster snapshot, which no reload survives. Served from the
            // persisted per-step attribution instead, so the number outlives the run.
            LlmDurationMs: timeSplit?.ModelMs ?? 0,
            ThrottleWaitMs: timeSplit?.ThrottleMs ?? 0,
            TicketId: string.IsNullOrEmpty(run.TicketId) ? null : run.TicketId,
            TicketTitle: run.TicketTitle,
            AgentName: run.AgentName,
            CancelRequested: run.CancelRequested,
            QueuePosition: queuePosition,
            ReservedGiMinutes: ReservedCapacityCalculator.Compute(run, orchestratorMemoryRequest),
            PendingQuestion: run.Status == "waiting_for_input" ? pendingQuestion : null,
            Footprint: RunFootprintView.From(capacity),
            // p0344b: beats always (list + detail); the story payloads only on
            // the detail path — the list stays lean.
            Beats: RunBeatsComputer.Compute(run),
            ProgressLedger: includeStory
                ? RunStoryJson.TryDeserialize<List<ProgressLedgerItemView>>(run.ProgressLedgerJson)
                : null,
            Acceptance: includeStory
                ? RunStoryJson.TryDeserialize<AcceptanceView>(run.AcceptanceJson)
                : null,
            // p0348: the pods actually spawned (persisted RunSandbox rows) — the
            // honest live-compute the side rail shows instead of the over-counting
            // reservation. Null until the first sandbox lands, and it persists
            // after the run because the rows do.
            LiveCompute: RunComputeView.From(run.Sandboxes),
            // p0350: every opened PR from run.Repos, so a multi-repo run shows all
            // of them (crash-resilient — recorded eagerly per-repo). This supersedes
            // p0347's PullRequestsFor read of Runs.PullRequestsJson for the snapshot;
            // that JSON still backs the Flatten /api/pull-requests page.
            PullRequests: openedPrs,
            // p0355: the typed cancel reason on the persisted row, so the UI can
            // distinguish a reap from an operator cancel.
            CancelReason: run.CancelReason,
            // p0357: the resolved budget (tier + cap) on list AND detail — the
            // spent/cap bar belongs on the run card as much as on the detail.
            BudgetTier: run.BudgetTier,
            BudgetCapUsd: run.BudgetCapUsd,
            BudgetCapTokens: run.BudgetCapTokens,
            // p0369: the folded metrics summary, detail-only (the top-N projection
            // of the stored fold). Null on the list path and on runs with no folded
            // events yet, so the client renders no metrics panel rather than zeros.
            Metrics: includeStory
                ? RunStoryJson.TryDeserialize<RunMetrics>(run.RunMetricsJson)?.ToView()
                : null,
            // p0404: the four-way split, detail-only — the run's answer to "where
            // did the wall-clock go", against which the rail's per-step splits sum.
            TimeSplit: includeStory ? timeSplit : null);
    }

}
