using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0200: HTTP control surface for in-flight pipeline runs. Today: cancel.
///
/// Three states the operator can hit:
///   1. Live run, executor await on the registry CTS — TryCancel signals
///      the per-run token; the executor's OCE catch publishes RunFinished
///      (status=failed, summary="cancelled"). Sandbox containers tear
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
        CancellationToken cancellationToken)
    {
        var liveCancel = registry.TryCancel(runId);
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

    private static Task PublishStaleClearAsync(
        string runId, IEventPublisher events, CancellationToken cancellationToken) =>
        events.PublishAsync(
            new RunFinishedEvent(
                RunId: runId,
                Status: "failed",
                PrUrl: null,
                Summary: "stale-cancelled (no executor was running this id)",
                FinishedAt: DateTimeOffset.UtcNow,
                CostUsd: null),
            cancellationToken);
}
