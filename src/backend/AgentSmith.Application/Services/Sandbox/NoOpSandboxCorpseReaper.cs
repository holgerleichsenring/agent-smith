using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0355: default corpse reaper for compositions with no pod backend (CLI / Docker
/// / InProcess) — there is nothing to reap. The Kubernetes backend swaps in the
/// real pod sweep.
/// </summary>
public sealed class NoOpSandboxCorpseReaper : ISandboxCorpseReaper
{
    public Task<int> ReapCorpsesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
}
