using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0320c: the ICapacityQueue facade for the singleton spawn/pump path. Like
/// <see cref="DbActiveRunLease"/>, it opens a SCOPE per operation and delegates
/// to the scoped <see cref="QueuedTicketRepository"/> so the DbContext stays
/// abstracted behind the scoped unit of work.
/// </summary>
public sealed class DbCapacityQueue(IServiceScopeFactory scopeFactory) : ICapacityQueue
{
    public Task<string> EnqueueAsync(CapacityQueueCandidate candidate, CancellationToken ct)
        => InScope(r => r.EnqueueAsync(candidate, ct));

    public Task<CapacityQueueEntry?> PeekHeadAsync(CancellationToken ct)
        => InScope(r => r.PeekHeadAsync(ct));

    public Task RemoveAsync(string project, string ticketId, CancellationToken ct)
        => InScope(r => r.RemoveAsync(project, ticketId, ct));

    public Task<int> CountAsync(CancellationToken ct)
        => InScope(r => r.CountAsync(ct));

    public Task<IReadOnlyDictionary<string, int>> GetPositionsByRunIdAsync(CancellationToken ct)
        => InScope(r => r.GetPositionsByRunIdAsync(ct));

    private async Task<T> InScope<T>(Func<QueuedTicketRepository, Task<T>> op)
    {
        using var scope = scopeFactory.CreateScope();
        return await op(scope.ServiceProvider.GetRequiredService<QueuedTicketRepository>());
    }

    private async Task InScope(Func<QueuedTicketRepository, Task> op)
    {
        using var scope = scopeFactory.CreateScope();
        await op(scope.ServiceProvider.GetRequiredService<QueuedTicketRepository>());
    }
}
