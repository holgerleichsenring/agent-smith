using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0336: the DB-free default (CLI / in-process / test composition). No ledger,
/// no gate — every footprint is admitted unconditionally, mirroring
/// UnboundedCapacityProbe. The Server replaces it with the DB-backed budget.
/// </summary>
public sealed class NoOpCapacityBudget : ICapacityBudget
{
    public Task RecordAsync(string runId, RunFootprintBreakdown footprint, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<bool> TryReserveAsync(string runId, CancellationToken ct) => Task.FromResult(true);

    public Task ReleaseAsync(string runId, CancellationToken ct) => Task.CompletedTask;

    public Task<RunCapacitySnapshot?> GetAsync(string runId, CancellationToken ct) =>
        Task.FromResult<RunCapacitySnapshot?>(null);

    public Task<IReadOnlyDictionary<string, RunCapacitySnapshot>> GetManyAsync(
        IReadOnlyCollection<string> runIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, RunCapacitySnapshot>>(
            new Dictionary<string, RunCapacitySnapshot>());
}
