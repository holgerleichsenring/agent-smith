using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0200: HTTP control surface for in-flight pipeline runs. Today: cancel.
///
/// Three states the operator can hit:
///   1. Live run, executor await on the registry CTS — TryCancel signals
///      the per-run token; the executor's OCE catch publishes RunFinished
///      (status=cancelled, summary="cancelled by operator"). Sandbox containers tear
///      down via PipelineExecutor's `await using var sandbox` → Docker
///      force-remove.
///   2. Stale snapshot in the dashboard's Active list with no executor
///      behind it (server restart killed the original run; the snapshot
///      survived because RunFinished never landed). TryCancel returns
///      false. We STILL publish RunCancelRequestedEvent + a synthetic
///      RunFinishedEvent(failed, "stale-cancelled") so the zombie clears
///      from Active and moves to Recent. Operator's intent — "make this
///      go away" — is honored regardless of whether there was anything
///      to cancel.
///   3. Genuinely unknown runId never broadcast at all → 404. Differentiated
///      from #2 by checking the broadcaster's active snapshot map.
/// </summary>
internal static class RunControlEndpoints
{
    internal static WebApplication MapRunControlEndpoints(this WebApplication app)
    {
        app.MapPost("/api/runs/{runId}/cancel", CancelAsync);
        return app;
    }

    private static async Task<IResult> CancelAsync(
        string runId,
        IRunCancellationRegistry registry,
        AgentSmith.Server.Services.Events.JobsBroadcaster broadcaster,
        IEventPublisher events,
        RunRepository runs,
        ICapacityQueue capacityQueue,
        CancellationToken cancellationToken)
    {
        // p0320c: a QUEUED run has no executor and holds no lease — cancelling it
        // is a pure bookkeeping move: delete the queue entry and finish the row
        // 'cancelled' via the terminal event (no registry roundtrip).
        if (await TryCancelQueuedAsync(runId, runs, capacityQueue, events, cancellationToken))
            return Results.Accepted();

        var liveCancel = registry.TryCancel(runId, reason: "operator");
        var snapshotExists = broadcaster.Active.ContainsKey(runId);
        if (!liveCancel && !snapshotExists) return Results.NotFound();

        await events.PublishAsync(
            new RunCancelRequestedEvent(runId, "operator", DateTimeOffset.UtcNow),
            cancellationToken);

        if (!liveCancel)
        {
            await PublishStaleClearAsync(runId, events, cancellationToken);
        }
        return Results.Accepted();
    }

    // Internal for the p0320c unit test (real repository over in-memory SQLite).
    internal static async Task<bool> TryCancelQueuedAsync(
        string runId, RunRepository runs, ICapacityQueue capacityQueue,
        IEventPublisher events, CancellationToken cancellationToken)
    {
        var run = await runs.GetRunDetailAsync(runId, cancellationToken);
        if (run is not { Status: "queued", FinishedAt: null }) return false;

        await capacityQueue.RemoveAsync(run.Project, run.TicketId, cancellationToken);
        await events.PublishAsync(
            new RunFinishedEvent(
                runId, "cancelled", null, "cancelled while queued (operator)",
                DateTimeOffset.UtcNow),
            cancellationToken);
        return true;
    }

    private static Task PublishStaleClearAsync(
        string runId, IEventPublisher events, CancellationToken cancellationToken) =>
        events.PublishAsync(
            new RunFinishedEvent(
                RunId: runId,
                // p0259: the operator's action was a cancel, so the zombie clears as
                // "cancelled" — consistent with a live cancel, not a spurious failure.
                Status: "cancelled",
                PrUrl: null,
                Summary: "stale-cancelled (no executor was running this id)",
                FinishedAt: DateTimeOffset.UtcNow,
                CostUsd: null),
            cancellationToken);
}
