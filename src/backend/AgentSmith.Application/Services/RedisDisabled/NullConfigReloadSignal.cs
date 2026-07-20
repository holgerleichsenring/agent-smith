using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.RedisDisabled;

/// <summary>
/// p0353: fallback IConfigReloadSignal for Redis-less graphs (CLI, tests, a
/// single-node server without Redis). The epoch never advances, so a leader that
/// resolves it simply never rebuilds — correct for a graph that has no cross-replica
/// concern. The import endpoint still resolves it and bumps a no-op.
/// </summary>
public sealed class NullConfigReloadSignal : IConfigReloadSignal
{
    public Task<long> CurrentEpochAsync(CancellationToken cancellationToken) => Task.FromResult(0L);

    public Task<long> BumpAsync(CancellationToken cancellationToken) => Task.FromResult(0L);
}
