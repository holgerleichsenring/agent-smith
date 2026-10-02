using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5f89e: the DB-free binding — a composition without the relational store has
/// no run row to renew. The server swaps in DbRunHeartbeat.
/// </summary>
public sealed class NoOpRunHeartbeat : IRunHeartbeat
{
    public Task RenewAsync(string runId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyCollection<string>> GetFreshRunIdsAsync(TimeSpan freshFor, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<string>>([]);

    public Task<IReadOnlyCollection<string>> GetFreshUnparkedRunIdsAsync(TimeSpan freshFor, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<string>>([]);
}
