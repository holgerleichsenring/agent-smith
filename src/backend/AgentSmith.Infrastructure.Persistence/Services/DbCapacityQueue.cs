using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
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

    public Task<CapacityQueueEntry?> FindAsync(string project, string ticketId, CancellationToken ct)
        => InScope(r => r.FindEntryAsync(project, ticketId, ct)); // 2026-11-26-a12eb

    // 2026-11-26-a12ec: the entry and its queued run row say why it waits now.
    public async Task ReasonAsync(string project, string ticketId, string reason, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var entry = uow.Set<QueuedTicket>().Where(q => q.Project == project && q.TicketId == ticketId);
        var runId = await entry.Select(q => q.ReservedRunId).FirstOrDefaultAsync(ct);
        await entry.ExecuteUpdateAsync(s => s.SetProperty(q => q.Reason, reason), ct);
        if (runId is not null)
            await uow.Set<Run>().Where(r => r.Id == runId && r.Status == RunStatuses.Queued).ExecuteUpdateAsync(s => s.SetProperty(r => r.Summary, reason), ct);
    }

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
