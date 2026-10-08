using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-08-0781: the ledger rows. The watermark only rises (one statement,
/// insert on no row, merge again on a racing insert); a spoken key is added once and reported
/// whether it was new. The worker holds the ticket's lock while it speaks, so keys never race.
/// </summary>
public sealed class ReworkLedgerRepository(IUnitOfWork uow, IUniqueViolationTranslator violations)
{
    private const int MaxKeys = 200;

    public async Task RaiseAsync(string project, string ticket, DateTimeOffset at, CancellationToken ct)
    {
        var ticks = at.UtcTicks;
        if (await RaiseRowAsync(project, ticket, ticks, ct) > 0) return;
        var entry = uow.Add(new ReworkLedger { Project = project, TicketId = ticket, NotServedThroughTicks = ticks });
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (violations.IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached;
            await RaiseRowAsync(project, ticket, ticks, ct);
        }
    }

    public async Task<DateTimeOffset?> WatermarkAsync(string project, string ticket, CancellationToken ct)
    {
        var ticks = await uow.Set<ReworkLedger>().AsNoTracking()
            .Where(l => l.Project == project && l.TicketId == ticket).Select(l => l.NotServedThroughTicks).FirstOrDefaultAsync(ct);
        return ticks is { } t ? new DateTimeOffset(t, TimeSpan.Zero) : null;
    }

    public async Task<bool> TryMarkSpokenAsync(string project, string ticket, string key, CancellationToken ct)
    {
        var row = await uow.Set<ReworkLedger>().FirstOrDefaultAsync(l => l.Project == project && l.TicketId == ticket, ct);
        if (row is null) uow.Add(row = new ReworkLedger { Project = project, TicketId = ticket });
        var keys = row.SpokenKeys.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (keys.Contains(key, StringComparer.Ordinal)) return false;
        keys.Add(key);
        row.SpokenKeys = string.Join('\n', keys.TakeLast(MaxKeys));
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private Task<int> RaiseRowAsync(string project, string ticket, long ticks, CancellationToken ct) =>
        uow.Set<ReworkLedger>().Where(l => l.Project == project && l.TicketId == ticket)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.NotServedThroughTicks,
                l => l.NotServedThroughTicks == null || l.NotServedThroughTicks < ticks ? ticks : l.NotServedThroughTicks), ct);
}
