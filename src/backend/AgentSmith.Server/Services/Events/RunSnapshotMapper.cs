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
    private const double BytesPerGi = 1024d * 1024d * 1024d;

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
        var openedPr = run.Repos.FirstOrDefault(r => r.PrStatus == "opened");
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
            TicketId: string.IsNullOrEmpty(run.TicketId) ? null : run.TicketId,
            TicketTitle: run.TicketTitle,
            AgentName: run.AgentName,
            CancelRequested: run.CancelRequested,
            QueuePosition: queuePosition,
            ReservedGiMinutes: ComputeReservedGiMinutes(run, orchestratorMemoryRequest),
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
                : null);
    }

    // p0332: RESERVED capacity-time — memory request x lifetime in Gi·minutes,
    // summed over the run's pods. Honest label: this is what the scheduler set
    // aside (what a requests-based quota counts), NOT measured consumption.
    // Only computed for finished runs; a sandbox that never got a close event
    // ends with the run (the pods are owner-referenced/disposed at run end).
    // Null when nothing is computable (pre-p0332 rows) — no fake zeros.
    private static double? ComputeReservedGiMinutes(Run run, string? orchestratorMemoryRequest)
    {
        if (run.FinishedAt is not { } finished) return null;

        var total = 0d;
        var any = false;
        foreach (var box in run.Sandboxes)
        {
            if (box.SpawnedAt is not { } spawned) continue; // pre-p0332 row
            var request = box.MemoryRequest ?? ResourceLimits.Default.MemoryRequest;
            if (!KubernetesQuantity.TryParseMemoryToBytes(request, out var bytes)) continue;
            total += GiMinutes(spawned, box.DisposedAt ?? finished, bytes);
            any = true;
        }

        // The spawned orchestrator (JobId set by p0330) lives for the whole run;
        // an in-process run (JobId null) has no orchestrator pod to account.
        var orchestratorRequest = orchestratorMemoryRequest ?? ResourceLimits.Default.MemoryRequest;
        if (run.JobId is not null
            && KubernetesQuantity.TryParseMemoryToBytes(orchestratorRequest, out var orchestratorBytes))
        {
            total += GiMinutes(run.StartedAt, finished, orchestratorBytes);
            any = true;
        }

        return any ? total : null;
    }

    private static double GiMinutes(DateTimeOffset from, DateTimeOffset to, long requestBytes) =>
        Math.Max(0d, (to - from).TotalMinutes) * (requestBytes / BytesPerGi);
}
