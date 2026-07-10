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
    public static RunSnapshot ToSnapshot(Run run, int? queuePosition = null)
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
            // The DB doesn't carry the producer's TotalSteps, so use the steps seen
            // (exact once finished; a lower bound while running). The live SignalR
            // path still carries the producer's value during an in-flight run.
            TotalSteps: run.Steps.Count,
            LastEventType: null,
            CostUsd: run.CostTotalUsd,
            LlmCalls: run.LlmCalls.Count,
            TicketId: string.IsNullOrEmpty(run.TicketId) ? null : run.TicketId,
            TicketTitle: run.TicketTitle,
            AgentName: run.AgentName,
            CancelRequested: run.CancelRequested,
            QueuePosition: queuePosition);
    }
}
