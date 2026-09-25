using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-09-25-b4d9: data access for the taken-ticket record over a scoped unit of work.
/// The state transitions are single UPDATE statements with the old state in the WHERE clause,
/// because the reaper runs on every replica and a read-then-write would let two passes both
/// believe they own the ticket.
/// </summary>
public sealed class TakenTicketRepository(IUnitOfWork unitOfWork)
{
    public async Task TakeAsync(TakenTicketFact fact, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var row = await RowsFor(fact.Project, fact.TicketId).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new TakenTicket { Project = fact.Project, TicketId = fact.TicketId };
            unitOfWork.Add(row);
        }
        row.Platform = fact.Platform;
        row.Pipeline = fact.Pipeline;
        row.State = (int)TakenTicketState.Taken;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(string project, string ticketId, CancellationToken ct) =>
        await RowsFor(project, ticketId).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<TakenTicketFact>> ListReconcilableAsync(CancellationToken ct) =>
        await unitOfWork.Set<TakenTicket>().AsNoTracking()
            .Where(t => t.State == (int)TakenTicketState.Taken)
            .Select(t => new TakenTicketFact(t.Project, t.TicketId, t.Platform, t.Pipeline))
            .ToListAsync(ct);

    public async Task<bool> TryBeginReapAsync(string project, string ticketId, CancellationToken ct)
    {
        var taken = await RowsFor(project, ticketId)
            .Where(t => t.State == (int)TakenTicketState.Taken)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.State, (int)TakenTicketState.Reaping), ct);
        // Nothing moved is two different facts: NO record (a lease from before this record
        // existed, or a ticket already finished — nobody owns it, so the reap proceeds), or a
        // record another pass is already holding (it does not).
        return taken > 0 || !await RowsFor(project, ticketId).AnyAsync(ct);
    }

    public async Task EndReapAsync(string project, string ticketId, CancellationToken ct) =>
        await RowsFor(project, ticketId)
            .Where(t => t.State == (int)TakenTicketState.Reaping)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.State, (int)TakenTicketState.Taken), ct);

    private IQueryable<TakenTicket> RowsFor(string project, string ticketId) =>
        unitOfWork.Set<TakenTicket>().Where(t => t.Project == project && t.TicketId == ticketId);
}
