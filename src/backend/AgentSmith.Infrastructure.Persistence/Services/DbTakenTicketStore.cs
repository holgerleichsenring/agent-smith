using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-09-25-b4d9: ITakenTicketStore facade for singleton callers (the claim, the reaper,
/// the reconciler). Like <see cref="DbUnmovedTicketStore"/>, it opens a scope per operation
/// and delegates to the scoped repository.
/// </summary>
public sealed class DbTakenTicketStore(IServiceScopeFactory scopeFactory) : ITakenTicketStore
{
    public Task TakeAsync(TakenTicketFact fact, CancellationToken cancellationToken) =>
        InScope(r => r.TakeAsync(fact, cancellationToken));

    public Task ClearAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        InScope(r => r.ClearAsync(project, ticketId, cancellationToken));

    public Task<IReadOnlyList<TakenTicketFact>> ListReconcilableAsync(CancellationToken cancellationToken) =>
        InScope(r => r.ListReconcilableAsync(cancellationToken));

    public Task<bool> TryBeginReapAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        InScope(r => r.TryBeginReapAsync(project, ticketId, cancellationToken));

    public Task EndReapAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        InScope(r => r.EndReapAsync(project, ticketId, cancellationToken));

    private async Task InScope(Func<TakenTicketRepository, Task> operation)
    {
        using var scope = scopeFactory.CreateScope();
        await operation(scope.ServiceProvider.GetRequiredService<TakenTicketRepository>());
    }

    private async Task<T> InScope<T>(Func<TakenTicketRepository, Task<T>> operation)
    {
        using var scope = scopeFactory.CreateScope();
        return await operation(scope.ServiceProvider.GetRequiredService<TakenTicketRepository>());
    }
}
