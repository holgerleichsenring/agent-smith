using System.Collections.Concurrent;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Events.Projection;

/// <summary>
/// 2026-10-26-0f19c: persists a drained event only on the replica that holds the projection lease. A
/// run is written by one task at a time; the first time a tenure meets a run — on its first event, or
/// when the tenure begins and the store calls the run unfinished — the run is caught up from the store,
/// and an event at or below the position written since is not written again. The fan-out is untouched:
/// every replica still shows its own clients every event.
/// </summary>
public sealed class LeaderGatedRunEventPersistence(
    IRunEventPersistence inner, ProjectionLeadership leadership, RunProjectionCatchUp catchUp)
    : IRunEventPersistence
{
    private readonly ConcurrentDictionary<string, RunProjectionState> _runs = new(StringComparer.Ordinal);

    public async Task PersistAsync(RunEvent runEvent, long position, CancellationToken cancellationToken)
    {
        if (!leadership.IsHolding) return;
        await UnderGateAsync(runEvent.RunId, async state =>
        {
            if (position <= state.Through) return;
            await inner.PersistAsync(runEvent, position, cancellationToken);
            state.Through = position;
        }, cancellationToken);
        if (runEvent is RunFinishedEvent finished && !RunStatuses.IsWaiting(finished.Status))
            _runs.TryRemove(runEvent.RunId, out _);
    }

    /// <summary>Catch one run up for the current tenure, if it has not been already.</summary>
    public Task SyncAsync(string runId, CancellationToken cancellationToken) =>
        leadership.IsHolding ? UnderGateAsync(runId, _ => Task.CompletedTask, cancellationToken) : Task.CompletedTask;

    private async Task UnderGateAsync(string runId, Func<RunProjectionState, Task> write, CancellationToken ct)
    {
        var state = _runs.GetOrAdd(runId, _ => new RunProjectionState());
        await state.Gate.WaitAsync(ct);
        try
        {
            var tenure = leadership.Tenure;
            if (state.Tenure != tenure)
            {
                state.Through = await catchUp.RunAsync(runId, inner, ct);
                state.Tenure = tenure;
            }
            await write(state);
        }
        finally { state.Gate.Release(); }
    }
}
