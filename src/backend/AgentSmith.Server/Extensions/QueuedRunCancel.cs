using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Lifecycle;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0320c: cancelling a run that never started. Pure bookkeeping — the queue entry is deleted,
/// the row is finished 'cancelled' and the ticket terminalized, with no executor to signal and
/// no lease to release. Split from <see cref="RunControlEndpoints"/> (2026-08-24-ca23), which
/// maps four handlers and could not take another responsibility without shedding one.
/// </summary>
internal static class QueuedRunCancel
{
    // Internal for the p0320c unit test (real repository over in-memory SQLite).
    internal static async Task<bool> TryAsync(
        string runId, RunRepository runs, ICapacityQueue capacityQueue,
        IEventPublisher events, CancelledTicketFinalizer ticketFinalizer,
        CancelTerminalWriter terminalWriter, CancellationToken cancellationToken)
    {
        var run = await runs.GetRunDetailAsync(runId, cancellationToken);
        if (run is not { Status: "queued", FinishedAt: null }) return false;

        await capacityQueue.RemoveAsync(run.Project, run.TicketId, cancellationToken);
        // 2026-08-24-ca23: the SECOND cancel entry point, with the same undrained stream as
        // the enforcer's — see CancelTerminalWriter.
        var terminal = new RunFinishedEvent(runId, "cancelled", null,
            "cancelled while queued (operator)", DateTimeOffset.UtcNow);
        await terminalWriter.FinalizeAsync(terminal, cancellationToken);
        await events.PublishAsync(terminal, cancellationToken);
        // p0330: the queue entry alone is not durable — the ticket still sits in
        // trigger_statuses and the next poll would re-claim it as a fresh run.
        // Terminalize it via the failed_status chain (fail-soft inside).
        await ticketFinalizer.FinalizeAsync(run.Project, run.TicketId, runId,
            "<b>Agent Smith — Cancelled</b><br/>Cancelled by operator while queued.",
            cancellationToken);
        return true;
    }
}
