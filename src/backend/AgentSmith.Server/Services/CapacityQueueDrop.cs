using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services;

namespace AgentSmith.Server.Services;

/// <summary>
/// 2026-09-21-c724c: ends a queued run the capacity-queue pump drops. Split out of
/// <see cref="CapacityQueuePump"/> — a drop now has an ORDER and four collaborators, which is
/// a responsibility of its own rather than a private helper on the dequeue loop.
/// <para>
/// The drop used to announce the end by PUBLISHING a terminal event. A run that never started
/// was never in the active set, so no cursor is created for its stream and nothing drains it
/// (<see cref="IRunListNudge"/> states the same gap): the entry vanished and the row went on
/// saying "queued" forever. It is WRITTEN through the same
/// <see cref="CancelTerminalWriter"/> both cancel entry points use, which goes through the
/// finalization projection that owns releasing capacity and computing the cost total.
/// </para>
/// <para>
/// The publish stays: the projection's set-once guard makes a later-drained event a harmless
/// no-op. The double capacity release is the same kind of no-op — the ledger releases by
/// DELETING the row for that run id, so the pump's own release and the projection's cannot
/// free capacity twice.
/// </para>
/// </summary>
public sealed class CapacityQueueDrop(
    ICapacityQueue queue,
    CancelTerminalWriter terminalWriter,
    IEventPublisher events,
    IRunListNudge runListNudge,
    ILogger<CapacityQueueDrop> logger)
{
    /// <summary>
    /// FINALIZE, then REMOVE, then ANNOUNCE — the two orders fail differently. Removing first
    /// and then failing to finalize reproduces exactly the state this exists to fix: entry
    /// gone, row queued, nothing left to retry. Finalizing first and then failing to remove
    /// leaves the head in place; the next tick peeks it, finalizes again as a no-op under the
    /// set-once guard, and retries the removal.
    /// </summary>
    public async Task DropAsync(CapacityQueueEntry head, string reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(head);
        var terminal = string.IsNullOrEmpty(head.ReservedRunId) ? null : new RunFinishedEvent(
            head.ReservedRunId!, "cancelled", null,
            $"dropped from capacity queue: {reason}", DateTimeOffset.UtcNow);
        if (terminal is not null) await terminalWriter.FinalizeAsync(terminal, ct);
        await queue.RemoveAsync(head.Project, head.TicketId, ct);
        if (terminal is not null)
        {
            await NudgeAsync(terminal.RunId, ct);
            await events.PublishAsync(terminal, ct);
        }
        logger.LogInformation(
            "Capacity-queue entry {Project}/#{Ticket} dropped: {Reason}",
            head.Project, head.TicketId, reason);
    }

    // A row written outside the run-event fanout is invisible to a connected dashboard until
    // something unrelated happens — the surface refetches on a nudge, on reconnect or on mount
    // and never polls. Best-effort, exactly as the deferral path nudges: a hub that is down
    // must not fail a drop whose row is already written and whose entry is already gone.
    private async Task NudgeAsync(string runId, CancellationToken ct)
    {
        try { await runListNudge.RunsChangedAsync(runId, ct); }
        catch (Exception ex) { logger.LogDebug(ex, "Queue-drop nudge failed for {RunId}", runId); }
    }
}
