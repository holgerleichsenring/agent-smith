using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-15-114fa: the close marks, served to singleton callers by opening a scope per operation. A set is an
/// upsert: an insert whose unique violation re-reads the winner's row and writes over it. When that re-read finds
/// nothing, a Reopen deleted the row between the two writes, and the insert is tried once more.
/// </summary>
public sealed class ClosedTicketRepository(IServiceScopeFactory scopeFactory) : ITicketClosedMarkStore
{
    public async Task SetAsync(TicketClosedMark mark, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mark);
        if (!await TrySetAsync(mark, cancellationToken) && !await TrySetAsync(mark, cancellationToken))
            throw new InvalidOperationException($"The close mark of {mark.TicketKey} on {mark.Tracker} kept vanishing while it was written.");
    }

    public async Task<bool> ClearAsync(string tracker, string ticketKey, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<ClosedTicket>()
            .Where(c => c.Tracker == tracker && c.TicketKey == ticketKey).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<TicketClosedMark>> OfAsync(
        string tracker, IReadOnlyCollection<string> ticketKeys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketKeys);
        if (ticketKeys.Count == 0) return [];
        var keys = ticketKeys.Distinct(StringComparer.Ordinal).ToList();
        using var scope = scopeFactory.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<ClosedTicket>().AsNoTracking()
            .Where(c => c.Tracker == tracker && keys.Contains(c.TicketKey)).OrderBy(c => c.Id).ToListAsync(cancellationToken);
        return [.. rows.Select(Mark)];
    }

    // False only when the winner's row was gone at the re-read.
    private async Task<bool> TrySetAsync(TicketClosedMark mark, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var row = await FindAsync(uow, mark, ct);
        if (row is null)
        {
            var entry = uow.Add(Write(new ClosedTicket(), mark));
            try
            {
                await uow.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex) when (scope.ServiceProvider.GetRequiredService<IUniqueViolationTranslator>().IsUniqueViolation(ex))
            {
                entry.State = EntityState.Detached;
                if ((row = await FindAsync(uow, mark, ct)) is null) return false;
            }
        }
        Write(row, mark);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private static Task<ClosedTicket?> FindAsync(IUnitOfWork uow, TicketClosedMark mark, CancellationToken ct) =>
        uow.Set<ClosedTicket>().FirstOrDefaultAsync(c => c.Tracker == mark.Tracker && c.TicketKey == mark.TicketKey, ct);

    private static ClosedTicket Write(ClosedTicket row, TicketClosedMark m)
    {
        (row.Tracker, row.TicketKey, row.TicketId, row.Project) = (m.Tracker, m.TicketKey, m.TicketId, m.Project);
        (row.ClosedAt, row.By, row.Note, row.TrackerClosed) = (m.ClosedAt, m.By, m.Note, m.TrackerClosed);
        row.ClosedPullRequestsJson = JsonSerializer.Serialize(m.ClosedPullRequests);
        return row;
    }

    private static TicketClosedMark Mark(ClosedTicket c) => new(c.Tracker, c.TicketKey, c.TicketId, c.Project, c.ClosedAt, c.By,
        c.Note, JsonSerializer.Deserialize<List<string>>(c.ClosedPullRequestsJson) ?? [], c.TrackerClosed);
}
