using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-09-18-c1a7: IUnmovedTicketStore facade for singleton callers. Like
/// <see cref="DbSpecSetPointerStore"/>, it opens a scope per operation and delegates to the
/// scoped repository.
/// </summary>
public sealed class DbUnmovedTicketStore(IServiceScopeFactory scopeFactory) : IUnmovedTicketStore
{
    public async Task RecordAsync(UnmovedTicketFact fact, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<UnmovedTicketRepository>()
            .RecordAsync(fact, cancellationToken);
    }

    public async Task<UnmovedTicketFact?> FindStandingAsync(
        string project, string ticketId, string tracker, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UnmovedTicketRepository>()
            .FindStandingAsync(project, ticketId, tracker, cancellationToken);
    }

    public async Task ClearAsync(string project, string ticketId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<UnmovedTicketRepository>()
            .ClearAsync(project, ticketId, cancellationToken);
    }
}
