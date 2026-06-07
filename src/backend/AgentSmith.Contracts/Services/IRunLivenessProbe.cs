using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// The reaper's POSITIVE-EVIDENCE source: asks the orchestrator whether a stale
/// lease's run is still present. Returns true when the container/pod is still
/// there (do NOT release) and false ONLY on positive evidence it is gone. The
/// safe default for an unknown handle is true — a lease is never released
/// without proof, so a flushed Redis / unreachable orchestrator never triggers a
/// mass release (the empty-Redis meltdown rule).
/// </summary>
public interface IRunLivenessProbe
{
    Task<bool> IsRunPresentAsync(StaleLease lease, CancellationToken cancellationToken);
}
