using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0327: IRunCheckpointStore facade for singleton callers (projector, sweeper,
/// endpoints). Like <see cref="DbCapacityQueue"/>, it opens a scope per
/// operation and delegates to the scoped repository.
/// </summary>
public sealed class DbRunCheckpointStore(IServiceScopeFactory scopeFactory) : IRunCheckpointStore
{
    public Task SaveAsync(RunCheckpointRecord checkpoint, CancellationToken ct)
        => InScope(r => r.SaveAsync(checkpoint, ct));

    public Task<RunCheckpointRecord?> GetByRunIdAsync(string runId, CancellationToken ct)
        => InScope(r => r.GetByRunIdAsync(runId, ct));

    public Task<IReadOnlyList<RunCheckpointRecord>> ListPendingAsync(CancellationToken ct)
        => InScope(r => r.ListPendingAsync(ct));

    public Task<bool> TryMarkResumedAsync(string runId, DateTimeOffset resumedAt, CancellationToken ct)
        => InScope(r => r.TryMarkResumedAsync(runId, resumedAt, ct));

    private async Task<T> InScope<T>(Func<RunCheckpointRepository, Task<T>> op)
    {
        using var scope = scopeFactory.CreateScope();
        return await op(scope.ServiceProvider.GetRequiredService<RunCheckpointRepository>());
    }

    private async Task InScope(Func<RunCheckpointRepository, Task> op)
    {
        using var scope = scopeFactory.CreateScope();
        await op(scope.ServiceProvider.GetRequiredService<RunCheckpointRepository>());
    }
}
