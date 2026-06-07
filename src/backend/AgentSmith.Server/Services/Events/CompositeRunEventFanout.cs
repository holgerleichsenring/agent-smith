using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Services;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// Fans every drained event to the SignalR hub (the inner fanout) AND — when
/// relational persistence is on — projects it to the DB. This is the
/// "server is the single writer" seam: the projector runs here, on the server's
/// consumption of the run stream, never in the spawned job. With no projector
/// (persistence off) it is a pure pass-through to the hub.
/// </summary>
public sealed class CompositeRunEventFanout(
    JobsHubFanout inner,
    RunDbProjector? projector) : IRunEventFanout
{
    public Task ToOverviewAsync(RunSnapshot snapshot, CancellationToken cancellationToken)
        => inner.ToOverviewAsync(snapshot, cancellationToken);

    public async Task ToRunAsync(string runId, RunEvent runEvent, CancellationToken cancellationToken)
    {
        if (projector is not null) await projector.ProjectAsync(runEvent, cancellationToken);
        await inner.ToRunAsync(runId, runEvent, cancellationToken);
    }

    // Deliberately does NOT project: ToSandboxAsync only carries the high-volume
    // SandboxOutput stdout stream (and only when a repo box is expanded). That is
    // ephemeral terminal output — persisting every line would dwarf the trail. The
    // structured events all flow through ToRunAsync, which the projector handles.
    public Task ToSandboxAsync(string runId, string repo, RunEvent runEvent, CancellationToken cancellationToken)
        => inner.ToSandboxAsync(runId, repo, runEvent, cancellationToken);

    public Task ToSystemAsync(SystemEvent systemEvent, CancellationToken cancellationToken)
        => inner.ToSystemAsync(systemEvent, cancellationToken);

    public Task ToSystemActivityAsync(SystemActivitySnapshot snapshot, CancellationToken cancellationToken)
        => inner.ToSystemActivityAsync(snapshot, cancellationToken);
}
