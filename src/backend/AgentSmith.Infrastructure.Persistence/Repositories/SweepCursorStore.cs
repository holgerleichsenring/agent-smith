using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-08-9e6e: the sweep cursors. A write is a compare-and-set that only moves forward, so a
/// leader that lost its lease mid-cycle can never move a cursor back; the first write inserts, and
/// a racing insert merges as a forward write.
/// </summary>
public sealed class SweepCursorStore(IUnitOfWork uow, IUniqueViolationTranslator violations) : ISweepCursors
{
    public async Task<SweepPosition?> GetAsync(string source, CancellationToken ct)
    {
        var row = await uow.Set<SweepCursor>().AsNoTracking().FirstOrDefaultAsync(c => c.Source == source, ct);
        return row is null ? null : new SweepPosition(new DateTimeOffset(row.AtTicks, TimeSpan.Zero), row.Resume);
    }

    public async Task AdvanceAsync(string source, SweepPosition position, CancellationToken ct)
    {
        if (await ForwardAsync(source, position, ct) > 0) return;
        // A row that refused the write is already ahead: never move it back.
        if (await uow.Set<SweepCursor>().AsNoTracking().AnyAsync(c => c.Source == source, ct)) return;
        var entry = uow.Add(new SweepCursor { Source = source, AtTicks = position.At.UtcTicks, Resume = position.Resume });
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (violations.IsUniqueViolation(ex))
        {
            entry.State = EntityState.Detached;
            await ForwardAsync(source, position, ct);
        }
    }

    private Task<int> ForwardAsync(string source, SweepPosition position, CancellationToken ct)
    {
        var ticks = position.At.UtcTicks;
        return uow.Set<SweepCursor>().Where(c => c.Source == source && c.AtTicks <= ticks)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.AtTicks, ticks).SetProperty(c => c.Resume, position.Resume), ct);
    }
}
