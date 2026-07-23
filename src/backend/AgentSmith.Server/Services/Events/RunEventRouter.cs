using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0367: the per-run event router — the single place that decides where a drained
/// event goes. Lifecycle/meaning events persist and fan to the Overview + Run
/// groups. Per-tool-call sandbox events (SandboxCommand/SandboxResult) persist
/// (batched, off the broadcast path) and surface only on the per-sandbox detail
/// group when a drawer is open, plus a coalesced liveness rollup to the Run group.
/// SandboxOutput is the ephemeral stdout stream: detail group only, never persisted.
/// </summary>
public sealed class RunEventRouter(
    IRunEventFanout fanout,
    SandboxExpansionRegistry expansionRegistry,
    SandboxDetailEventClassifier classifier,
    SandboxActivityCoalescer coalescer,
    IRunEventPersistence persistence)
{
    public async Task DispatchAsync(
        string runId, RunSnapshot snapshot, RunEvent runEvent, CancellationToken cancellationToken)
    {
        if (classifier.IsSandboxDetailOnly(runEvent.Type))
        {
            await RouteSandboxDetailAsync(runId, runEvent, cancellationToken);
            return;
        }

        await persistence.PersistAsync(runEvent, cancellationToken);
        await fanout.ToOverviewAsync(snapshot, cancellationToken);
        await fanout.ToRunAsync(runId, runEvent, cancellationToken);
    }

    private async Task RouteSandboxDetailAsync(string runId, RunEvent runEvent, CancellationToken ct)
    {
        // SandboxOutput is transient terminal noise — never persisted. The tool-call
        // pair IS persisted (metrics), but batched and off the Run-group send.
        if (runEvent.Type != EventType.SandboxOutput)
            await persistence.PersistAsync(runEvent, ct);

        if (runEvent is SandboxCommandEvent command)
        {
            var rollup = coalescer.Observe(runId, command);
            if (rollup is not null) await fanout.ToRunActivityAsync(runId, rollup, ct);
        }

        var repo = RepoOf(runEvent);
        if (repo is not null && expansionRegistry.IsExpanded(runId, repo))
            await fanout.ToSandboxAsync(runId, repo, runEvent, ct);
    }

    private static string? RepoOf(RunEvent runEvent) => runEvent switch
    {
        SandboxCommandEvent e => e.Repo,
        SandboxOutputEvent e => e.Repo,
        SandboxResultEvent e => e.Repo,
        _ => null,
    };
}
