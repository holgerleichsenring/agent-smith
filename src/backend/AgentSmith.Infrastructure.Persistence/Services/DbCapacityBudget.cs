using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0336: the ICapacityBudget facade for the singleton spawn/pump path. Opens a
/// SCOPE per operation and delegates to the scoped RunCapacityRepository (the
/// DbCapacityQueue pattern). The configured budget is parsed once to nanos/bytes;
/// null (unset) means unbounded — footprints are still recorded for the dashboard,
/// but every reservation is admitted (fail-open, so a missing budget never wedges
/// all runs). The k8s ResourceQuota remains the hard backstop.
/// </summary>
public sealed class DbCapacityBudget(
    IServiceScopeFactory scopeFactory, IOptions<CapacityBudgetOptions> options) : ICapacityBudget
{
    private readonly long? _budgetCpuNanos =
        KubernetesQuantity.TryParseCpuToNanoCpus(options.Value.CpuLimit, out var c) ? c : null;
    private readonly long? _budgetMemBytes =
        KubernetesQuantity.TryParseMemoryToBytes(options.Value.MemoryLimit, out var m) ? m : null;

    public Task RecordAsync(string runId, RunFootprintBreakdown footprint, CancellationToken ct) =>
        InScope(r => r.UpsertFootprintAsync(
            runId, JsonSerializer.Serialize(footprint), footprint.TotalCpuNanos, footprint.TotalMemBytes, ct));

    public Task<bool> TryReserveAsync(string runId, CancellationToken ct) =>
        InScope(r => r.TryReserveAsync(runId, _budgetCpuNanos, _budgetMemBytes, ct));

    public Task ReleaseAsync(string runId, CancellationToken ct) =>
        InScope(r => r.ReleaseAsync(runId, ct));

    public async Task<RunCapacitySnapshot?> GetAsync(string runId, CancellationToken ct)
    {
        var row = await InScope(r => r.GetAsync(runId, ct));
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<IReadOnlyDictionary<string, RunCapacitySnapshot>> GetManyAsync(
        IReadOnlyCollection<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return new Dictionary<string, RunCapacitySnapshot>();
        var rows = await InScope(r => r.GetManyAsync(runIds, ct));
        return rows.ToDictionary(r => r.RunId, ToSnapshot);
    }

    private static RunCapacitySnapshot ToSnapshot(Entities.RunCapacity row) =>
        new(JsonSerializer.Deserialize<RunFootprintBreakdown>(row.FootprintJson)
            ?? RunFootprintBreakdown.Empty, row.Reserved);

    private async Task<T> InScope<T>(Func<RunCapacityRepository, Task<T>> op)
    {
        using var scope = scopeFactory.CreateScope();
        return await op(scope.ServiceProvider.GetRequiredService<RunCapacityRepository>());
    }

    private async Task InScope(Func<RunCapacityRepository, Task> op)
    {
        using var scope = scopeFactory.CreateScope();
        await op(scope.ServiceProvider.GetRequiredService<RunCapacityRepository>());
    }
}
